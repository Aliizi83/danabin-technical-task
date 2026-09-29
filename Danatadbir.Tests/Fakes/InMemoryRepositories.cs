using Danatadbir.Domain.Alerting;
using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;

namespace Danatadbir.Tests.Fakes;

/// <summary>
/// In-memory stand-ins for the stores. Each one enforces the same natural key as the real store
/// (an InfluxDB point identity, a unique index), so an idempotency test against them says the same
/// thing it would say against the databases.
/// </summary>
public class InMemorySensorRepository(params string[] ids) : ISensorRepository
{
    public Task<List<Sensor>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ids.Select((id, index) => new Sensor { Id = index + 1, ExternalId = id, Title = id }).ToList());
}

public class InMemoryMetricRepository(params string[] keys) : IMetricRepository
{
    public Task<List<Metric>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(keys.Select((key, index) => new Metric { Id = index + 1, Key = key, Title = key }).ToList());
}

public class InMemorySensorDataRepository : ISensorDataRepository
{
    public Dictionary<(string, string, DateTime, long), SensorData> Points { get; } = [];

    public int WriteCalls { get; private set; }

    public Task WriteAsync(IReadOnlyCollection<SensorData> readings, CancellationToken cancellationToken = default)
    {
        WriteCalls++;

        foreach (var reading in readings)
            Points[reading.Identity] = reading;

        return Task.CompletedTask;
    }

    public Task<List<SensorData>> GetRangeAsync(
        string sensorExternalId, string metricKey, DateTime from, DateTime to,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Points.Values
            .Where(r => r.SensorExternalId == sensorExternalId && r.MetricKey == metricKey
                        && r.Timestamp >= from && r.Timestamp < to)
            .ToList());
}

public class InMemoryRuleResultRepository : IRuleResultRepository
{
    public List<RuleResult> Rows { get; } = [];

    public Task<RuleResultSyncOutcome> SyncAsync(
        IReadOnlyCollection<SensorData> evaluated,
        IReadOnlyCollection<RuleResult> results,
        CancellationToken cancellationToken = default)
    {
        var identities = evaluated.Select(r => r.Identity).ToHashSet();
        var wanted = results.ToDictionary(Key, r => r.Value);

        var stale = Rows
            .Where(row => identities.Contains((row.SensorExternalId, row.MetricKey, row.Timestamp, row.Seq)))
            .Where(row => !wanted.TryGetValue(Key(row), out var value) || value != row.Value)
            .ToList();

        foreach (var row in stale)
            Rows.Remove(row);

        var present = Rows.Select(Key).ToHashSet();
        var missing = results.Where(r => !present.Contains(Key(r))).ToList();
        Rows.AddRange(missing);

        return Task.FromResult(new RuleResultSyncOutcome(missing.Count, stale.Count));
    }

    public Task<List<RuleResult>> GetViolationsAsync(
        string sensorExternalId, string metricKey, DateTime from, DateTime to,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows
            .Where(r => r.SensorExternalId == sensorExternalId && r.MetricKey == metricKey
                        && r.Timestamp >= from && r.Timestamp < to)
            .ToList());

    private static (string, string, DateTime, long, string) Key(RuleResult r) =>
        (r.SensorExternalId, r.MetricKey, r.Timestamp, r.Seq, r.RuleId);
}

public class InMemoryAlertRepository : IAlertRepository
{
    public List<Alert> Rows { get; } = [];

    public Task<IReadOnlyDictionary<AlertSeriesKey, IReadOnlyList<DateTime>>> GetAlertStartsAsync(
        IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<AlertSeriesKey, IReadOnlyList<DateTime>>>(Rows
            .Where(a => ruleIds.Contains(a.RuleId))
            .GroupBy(a => new AlertSeriesKey(a.RuleId, a.SensorExternalId, a.MetricKey))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DateTime>)g.Select(a => a.StartTs).Order().ToList()));

    public Task<int> AddMissingAsync(IReadOnlyCollection<Alert> alerts, CancellationToken cancellationToken = default)
    {
        var present = Rows.Select(Key).ToHashSet();
        var missing = alerts.Where(a => present.Add(Key(a))).ToList();
        Rows.AddRange(missing);
        return Task.FromResult(missing.Count);
    }

    public Task<List<Alert>> QueryAsync(
        string? sensorExternalId, string? metricKey, string? ruleId, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows
            .Where(a => sensorExternalId is null || a.SensorExternalId == sensorExternalId)
            .Where(a => metricKey is null || a.MetricKey == metricKey)
            .Where(a => ruleId is null || a.RuleId == ruleId)
            .Where(a => from is null || a.StartTs >= from)
            .Where(a => to is null || a.StartTs < to)
            .OrderBy(a => a.StartTs).ToList());

    private static (string, string, string, DateTime) Key(Alert a) => (a.RuleId, a.SensorExternalId, a.MetricKey, a.StartTs);
}
