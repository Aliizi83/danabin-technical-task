using Danatadbir.Domain.Alerting;
using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Repositories;

public interface IAlertRepository
{
    Task<IReadOnlyDictionary<AlertSeriesKey, IReadOnlyList<DateTime>>> GetAlertStartsAsync(
        IReadOnlyCollection<string> ruleIds,
        CancellationToken cancellationToken = default);

    Task<int> AddMissingAsync(IReadOnlyCollection<Alert> alerts, CancellationToken cancellationToken = default);

    /// <summary>Alerts matching every given filter; alerts whose start lies in [from, to).</summary>
    Task<List<Alert>> QueryAsync(
        string? sensorExternalId,
        string? metricKey,
        string? ruleId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}
