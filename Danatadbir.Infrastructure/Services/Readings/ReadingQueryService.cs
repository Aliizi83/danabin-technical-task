using System.Net;
using Danatadbir.Application.Common.Result;
using Danatadbir.Application.ReadingService;
using Danatadbir.Application.ReadingService.Dtos;
using Danatadbir.Domain.Aggregation;
using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;

namespace Danatadbir.Infrastructure.Services.Readings;

/// <summary>
/// Reads back what ingestion stored. A reading is unacceptable when it has at least one stored rule
/// violation and acceptable otherwise, so the two lists partition the stored readings and the
/// aggregation, which uses the acceptable side only, can never count a violating reading.
/// </summary>
public class ReadingQueryService(
    ISensorRepository sensorRepository,
    IMetricRepository metricRepository,
    ISensorDataRepository sensorDataRepository,
    IRuleResultRepository ruleResultRepository) : IReadingQueryService
{
    private const int MaxBuckets = 10_000;
    private const int MaxPageSize = 1000;

    private record Target(string Sensor, string Metric, DateTime From, DateTime To);

    public async Task<BaseResult<AggregationResultDto>> AggregateAsync(
        AggregationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var (target, failure) = await ResolveAsync(query.DeviceId, query.Metric, query.From, query.To, cancellationToken);

        if (target is null)
            return BaseResult<AggregationResultDto>.Failure(failure!.Value.Status, failure.Value.Message);

        if (query.BucketSeconds <= 0)
            return BaseResult<AggregationResultDto>.Failure(HttpStatusCode.BadRequest, "bucketSeconds must be greater than zero.");

        var bucketSize = TimeSpan.FromSeconds(query.BucketSeconds);
        var bucketCount = Math.Ceiling((target.To - target.From) / bucketSize);

        if (bucketCount > MaxBuckets)
        {
            return BaseResult<AggregationResultDto>.Failure(
                HttpStatusCode.BadRequest,
                $"The range holds {bucketCount:N0} buckets; at most {MaxBuckets:N0} are allowed. Use a larger bucketSeconds or a shorter range.");
        }

        var (acceptable, unacceptable) = await LoadAsync(target, cancellationToken);

        var buckets = TimeBucketAggregator
            .Aggregate(acceptable, target.From, target.To, bucketSize)
            .Select(bucket => new BucketDto(bucket.Start, bucket.Count, bucket.Average, bucket.Min, bucket.Max))
            .ToList();

        return BaseResult<AggregationResultDto>.Ok(new AggregationResultDto(
            target.Sensor, target.Metric, target.From, target.To, query.BucketSeconds,
            acceptable.Count, unacceptable.Count, buckets));
    }

    public async Task<BaseResult<List<AcceptableReadingDto>>> GetAcceptableAsync(
        ReadingsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var (target, failure) = await ResolveAsync(query.DeviceId, query.Metric, query.From, query.To, cancellationToken);

        if (target is null)
            return BaseResult<List<AcceptableReadingDto>>.Failure(failure!.Value.Status, failure.Value.Message);

        if (PagingError(query.Page, query.PageSize) is { } error)
            return BaseResult<List<AcceptableReadingDto>>.Failure(HttpStatusCode.BadRequest, error);

        var (acceptable, _) = await LoadAsync(target, cancellationToken);

        var page = acceptable
            .OrderBy(reading => reading.Timestamp).ThenBy(reading => reading.Seq)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(reading => new AcceptableReadingDto(reading.Timestamp, reading.Value, reading.Seq))
            .ToList();

        return BaseResult<List<AcceptableReadingDto>>.Ok(page, Paging(query, acceptable.Count));
    }

    public async Task<BaseResult<List<UnacceptableReadingDto>>> GetUnacceptableAsync(
        ReadingsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var (target, failure) = await ResolveAsync(query.DeviceId, query.Metric, query.From, query.To, cancellationToken);

        if (target is null)
            return BaseResult<List<UnacceptableReadingDto>>.Failure(failure!.Value.Status, failure.Value.Message);

        if (PagingError(query.Page, query.PageSize) is { } error)
            return BaseResult<List<UnacceptableReadingDto>>.Failure(HttpStatusCode.BadRequest, error);

        var violations = await ruleResultRepository.GetViolationsAsync(
            target.Sensor, target.Metric, target.From, target.To, cancellationToken);

        var readings = violations
            .GroupBy(violation => (violation.Timestamp, violation.Seq))
            .OrderBy(group => group.Key.Timestamp).ThenBy(group => group.Key.Seq)
            .Select(group => new UnacceptableReadingDto(
                group.Key.Timestamp,
                group.First().Value,
                group.Key.Seq,
                group.Select(violation => new ViolatedRuleDto(violation.RuleId, violation.RuleName, violation.Reason)).ToList()))
            .ToList();

        var page = readings.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return BaseResult<List<UnacceptableReadingDto>>.Ok(page, Paging(query, readings.Count));
    }

    /// <summary>Acceptable readings come from the time-series store minus those with a stored violation.</summary>
    private async Task<(List<SensorData> Acceptable, List<RuleResult> Unacceptable)> LoadAsync(
        Target target,
        CancellationToken cancellationToken)
    {
        var readings = await sensorDataRepository.GetRangeAsync(
            target.Sensor, target.Metric, target.From, target.To, cancellationToken);

        var violations = await ruleResultRepository.GetViolationsAsync(
            target.Sensor, target.Metric, target.From, target.To, cancellationToken);

        var violated = violations.Select(violation => (violation.Timestamp, violation.Seq)).ToHashSet();

        var acceptable = readings.Where(reading => !violated.Contains((reading.Timestamp, reading.Seq))).ToList();

        return (acceptable, violations.GroupBy(violation => (violation.Timestamp, violation.Seq)).Select(g => g.First()).ToList());
    }

    private async Task<(Target? Target, (HttpStatusCode Status, string Message)? Failure)> ResolveAsync(
        string? deviceId,
        string? metric,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return (null, (HttpStatusCode.BadRequest, "deviceId is required."));

        if (string.IsNullOrWhiteSpace(metric))
            return (null, (HttpStatusCode.BadRequest, "metric is required."));

        from = ToUtc(from);
        to = ToUtc(to);

        if (from >= to)
            return (null, (HttpStatusCode.BadRequest, "from must be earlier than to."));

        var sensor = (await sensorRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(candidate => string.Equals(candidate.ExternalId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));

        if (sensor is null)
            return (null, (HttpStatusCode.NotFound, $"Sensor '{deviceId}' is not registered."));

        var registeredMetric = (await metricRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(candidate => string.Equals(candidate.Key, metric.Trim(), StringComparison.OrdinalIgnoreCase));

        if (registeredMetric is null)
            return (null, (HttpStatusCode.NotFound, $"Metric '{metric}' is not registered."));

        return (new Target(sensor.ExternalId, registeredMetric.Key, from, to), null);
    }

    private static string? PagingError(int page, int pageSize) =>
        page < 1 ? "page must be at least 1."
        : pageSize is < 1 or > MaxPageSize ? $"pageSize must be between 1 and {MaxPageSize}."
        : null;

    private static PaginationMetaData Paging(ReadingsQueryDto query, int total) =>
        new() { PageNumber = query.Page, PageSize = query.PageSize, TotalCount = total };

    /// <summary>A value without an offset is taken as UTC, like the feed itself.</summary>
    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
        : value.ToUniversalTime();
}
