using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Repositories;

public record AlertSeriesKey(string RuleId, string SensorExternalId, string MetricKey);

public interface IAlertRepository
{
    Task<IReadOnlyDictionary<AlertSeriesKey, IReadOnlyList<DateTime>>> GetAlertStartsAsync(
        IReadOnlyCollection<string> ruleIds,
        CancellationToken cancellationToken = default);

    Task<int> AddMissingAsync(IReadOnlyCollection<Alert> alerts, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
