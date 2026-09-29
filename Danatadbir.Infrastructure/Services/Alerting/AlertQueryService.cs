using System.Net;
using Danatadbir.Application.AlertService;
using Danatadbir.Application.AlertService.Dtos;
using Danatadbir.Application.Common.Result;
using Danatadbir.Domain.Repositories;

namespace Danatadbir.Infrastructure.Services.Alerting;

public class AlertQueryService(
    ISensorRepository sensorRepository,
    IMetricRepository metricRepository,
    IAlertRepository alertRepository) : IAlertQueryService
{
    private const int MaxPageSize = 1000;

    public async Task<BaseResult<List<AlertDto>>> GetAsync(
        string? deviceId,
        string? metric,
        string? ruleId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return BaseResult<List<AlertDto>>.Failure(
                HttpStatusCode.BadRequest, $"page must be at least 1 and pageSize between 1 and {MaxPageSize}.");
        }

        string? sensorId = null;
        string? metricKey = null;

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            sensorId = (await sensorRepository.GetAllAsync(cancellationToken))
                .FirstOrDefault(sensor => string.Equals(sensor.ExternalId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase))
                ?.ExternalId;

            if (sensorId is null)
                return BaseResult<List<AlertDto>>.Failure(HttpStatusCode.NotFound, $"Sensor '{deviceId}' is not registered.");
        }

        if (!string.IsNullOrWhiteSpace(metric))
        {
            metricKey = (await metricRepository.GetAllAsync(cancellationToken))
                .FirstOrDefault(candidate => string.Equals(candidate.Key, metric.Trim(), StringComparison.OrdinalIgnoreCase))
                ?.Key;

            if (metricKey is null)
                return BaseResult<List<AlertDto>>.Failure(HttpStatusCode.NotFound, $"Metric '{metric}' is not registered.");
        }

        var alerts = await alertRepository.QueryAsync(
            sensorId, metricKey, string.IsNullOrWhiteSpace(ruleId) ? null : ruleId.Trim(),
            ToUtc(from), ToUtc(to), cancellationToken);

        var items = alerts
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(alert => new AlertDto(
                alert.RuleId, alert.RuleName, alert.SensorExternalId, alert.MetricKey,
                alert.StartTs, alert.EndTs, alert.PeakValue, alert.ReadingCount))
            .ToList();

        return BaseResult<List<AlertDto>>.Ok(
            items, new PaginationMetaData { PageNumber = page, PageSize = pageSize, TotalCount = alerts.Count });
    }

    private static DateTime? ToUtc(DateTime? value) => value is null
        ? null
        : value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
}
