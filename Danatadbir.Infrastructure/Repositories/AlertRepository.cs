using Danatadbir.Domain.Alerting;
using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;
using Danatadbir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Danatadbir.Infrastructure.Repositories;

public class AlertRepository(AppDbContext context) : IAlertRepository
{
    public async Task<IReadOnlyDictionary<AlertSeriesKey, IReadOnlyList<DateTime>>> GetAlertStartsAsync(
        IReadOnlyCollection<string> ruleIds,
        CancellationToken cancellationToken = default)
    {
        if (ruleIds.Count == 0)
            return new Dictionary<AlertSeriesKey, IReadOnlyList<DateTime>>();

        var rows = await context.Alerts
            .AsNoTracking()
            .Where(alert => ruleIds.Contains(alert.RuleId))
            .Select(alert => new { alert.RuleId, alert.SensorExternalId, alert.MetricKey, alert.StartTs })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new AlertSeriesKey(row.RuleId, row.SensorExternalId, row.MetricKey))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DateTime>)group.Select(row => row.StartTs).Order().ToList());
    }

    public async Task<int> AddMissingAsync(
        IReadOnlyCollection<Alert> alerts,
        CancellationToken cancellationToken = default)
    {
        if (alerts.Count == 0)
            return 0;

        var ruleIds = alerts.Select(alert => alert.RuleId).Distinct().ToList();

        var existing = await context.Alerts
            .AsNoTracking()
            .Where(alert => ruleIds.Contains(alert.RuleId))
            .Select(alert => new { alert.RuleId, alert.SensorExternalId, alert.MetricKey, alert.StartTs })
            .ToListAsync(cancellationToken);

        var known = existing
            .Select(alert => (alert.RuleId, alert.SensorExternalId, alert.MetricKey, alert.StartTs))
            .ToHashSet();

        var missing = alerts
            .Where(alert => known.Add((alert.RuleId, alert.SensorExternalId, alert.MetricKey, alert.StartTs)))
            .ToList();

        if (missing.Count == 0)
            return 0;

        await context.Alerts.AddRangeAsync(missing, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return missing.Count;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        context.Alerts.CountAsync(cancellationToken);
}
