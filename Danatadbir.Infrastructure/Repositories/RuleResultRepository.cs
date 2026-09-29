using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;
using Danatadbir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Danatadbir.Infrastructure.Repositories;

public class RuleResultRepository(AppDbContext context) : IRuleResultRepository
{
    private record ResultKey(string Sensor, string Metric, DateTime Timestamp, long Seq, string RuleId);

    public async Task<RuleResultSyncOutcome> SyncAsync(
        IReadOnlyCollection<SensorData> evaluated,
        IReadOnlyCollection<RuleResult> results,
        CancellationToken cancellationToken = default)
    {
        if (evaluated.Count == 0)
            return new RuleResultSyncOutcome(0, 0);

        var evaluatedIdentities = evaluated.Select(reading => reading.Identity).ToHashSet();
        var sensors = evaluated.Select(reading => reading.SensorExternalId).Distinct().ToList();
        var first = evaluated.Min(reading => reading.Timestamp);
        var last = evaluated.Max(reading => reading.Timestamp);

        // One round trip for everything stored in the affected range, not a query per reading.
        var stored = await context.RuleResults
            .AsNoTracking()
            .Where(row => sensors.Contains(row.SensorExternalId) && row.Timestamp >= first && row.Timestamp <= last)
            .Select(row => new
            {
                row.Id, row.SensorExternalId, row.MetricKey, row.Timestamp, row.Seq, row.RuleId, row.Value
            })
            .ToListAsync(cancellationToken);

        var wanted = results.ToDictionary(Key, result => result.Value);

        var staleIds = stored
            .Where(row => evaluatedIdentities.Contains((row.SensorExternalId, row.MetricKey, row.Timestamp, row.Seq)))
            .Where(row =>
                !wanted.TryGetValue(new ResultKey(row.SensorExternalId, row.MetricKey, row.Timestamp, row.Seq, row.RuleId), out var value)
                || value != row.Value)
            .Select(row => row.Id)
            .ToList();

        if (staleIds.Count > 0)
        {
            // A hard delete: violations are derived data, and a soft-deleted row would keep
            // occupying the unique key the corrected violation needs.
            await context.RuleResults.IgnoreQueryFilters()
                .Where(row => staleIds.Contains(row.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var remaining = stored
            .Where(row => !staleIds.Contains(row.Id))
            .Select(row => new ResultKey(row.SensorExternalId, row.MetricKey, row.Timestamp, row.Seq, row.RuleId))
            .ToHashSet();

        var missing = results.Where(result => !remaining.Contains(Key(result))).ToList();

        if (missing.Count > 0)
        {
            await context.RuleResults.AddRangeAsync(missing, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        return new RuleResultSyncOutcome(missing.Count, staleIds.Count);
    }

    public Task<List<RuleResult>> GetViolationsAsync(
        string sensorExternalId,
        string metricKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default) =>
        context.RuleResults
            .AsNoTracking()
            .Where(row => row.SensorExternalId == sensorExternalId
                          && row.MetricKey == metricKey
                          && row.Timestamp >= from
                          && row.Timestamp < to)
            .ToListAsync(cancellationToken);

    private static ResultKey Key(RuleResult result) =>
        new(result.SensorExternalId, result.MetricKey, result.Timestamp, result.Seq, result.RuleId);
}
