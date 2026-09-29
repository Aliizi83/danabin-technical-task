using System.Net;
using Danatadbir.Application.AlertService;
using Danatadbir.Application.AlertService.Dtos;
using Danatadbir.Application.Common;
using Danatadbir.Application.Common.Result;
using Danatadbir.Domain.Repositories;

namespace Danatadbir.Infrastructure.Services.Alerting;

public class AlertQueryService(
    ISensorRepository sensorRepository,
    IMetricRepository metricRepository,
    IAlertRepository alertRepository) : IAlertQueryService
{
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
        if (Paging.Validate(page, pageSize) is { } error)
            return BaseResult<List<AlertDto>>.Failure(HttpStatusCode.BadRequest, error);

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

        // Ordered by the full key here, not left to the database: two alerts of one rule can start at
        // the same time on different sensors, and a page boundary must fall the same way every call.
        var ordered = alerts
            .OrderBy(alert => alert.StartTs).ThenBy(alert => alert.RuleId)
            .ThenBy(alert => alert.SensorExternalId, StringComparer.Ordinal).ThenBy(alert => alert.MetricKey, StringComparer.Ordinal)
            .Select(alert => new AlertDto(
                alert.RuleId, alert.RuleName, alert.SensorExternalId, alert.MetricKey,
                alert.StartTs, alert.EndTs, alert.PeakValue, alert.ReadingCount))
            .ToList();

        return BaseResult<List<AlertDto>>.Ok(
            Paging.Slice(ordered, page, pageSize), Paging.Meta(page, pageSize, ordered.Count));
    }

    private static DateTime? ToUtc(DateTime? value) => value is null
        ? null
        : value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
}
