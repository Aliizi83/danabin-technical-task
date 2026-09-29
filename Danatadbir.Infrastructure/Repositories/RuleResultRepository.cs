using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;
using Danatadbir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Danatadbir.Infrastructure.Repositories;

public class RuleResultRepository(AppDbContext context) : IRuleResultRepository
{
    public async Task<int> AddMissingAsync(
        IReadOnlyCollection<RuleResult> results,
        CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
            return 0;

        // One round trip to find what is already stored, rather than a query per violation.
        var sensors = results.Select(result => result.SensorExternalId).Distinct().ToList();
        var timestamps = results.Select(result => result.Timestamp).Distinct().ToList();

        var existing = await context.RuleResults
            .AsNoTracking()
            .Where(result => sensors.Contains(result.SensorExternalId) && timestamps.Contains(result.Timestamp))
            .Select(result => new
            {
                result.SensorExternalId,
                result.MetricKey,
                result.Timestamp,
                result.Seq,
                result.RuleId
            })
            .ToListAsync(cancellationToken);

        var known = existing
            .Select(result => (result.SensorExternalId, result.MetricKey, result.Timestamp, result.Seq, result.RuleId))
            .ToHashSet();

        var missing = results
            .Where(result => known.Add(
                (result.SensorExternalId, result.MetricKey, result.Timestamp, result.Seq, result.RuleId)))
            .ToList();

        if (missing.Count == 0)
            return 0;

        await context.RuleResults.AddRangeAsync(missing, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return missing.Count;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        context.RuleResults.CountAsync(cancellationToken);
}
