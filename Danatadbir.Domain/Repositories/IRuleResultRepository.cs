using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Repositories;

public record RuleResultSyncOutcome(int Inserted, int Removed);

public interface IRuleResultRepository
{
    /// <summary>
    /// Makes the stored violations of the given readings match <paramref name="results"/>: missing
    /// ones are inserted, and stored ones that no longer hold (the rule stopped firing, or the
    /// reading now has another value) are removed. Readings not in <paramref name="evaluated"/> are
    /// left alone. Running it twice with the same input inserts and removes nothing.
    /// </summary>
    Task<RuleResultSyncOutcome> SyncAsync(
        IReadOnlyCollection<SensorData> evaluated,
        IReadOnlyCollection<RuleResult> results,
        CancellationToken cancellationToken = default);

    /// <summary>Violations of one series with a timestamp in [from, to).</summary>
    Task<List<RuleResult>> GetViolationsAsync(
        string sensorExternalId,
        string metricKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
