using Danatadbir.Domain.Alerting;
using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Repositories;

public interface IAlertRepository
{
    Task<IReadOnlyDictionary<AlertSeriesKey, IReadOnlyList<DateTime>>> GetAlertStartsAsync(
        IReadOnlyCollection<string> ruleIds,
        CancellationToken cancellationToken = default);

    Task<int> AddMissingAsync(IReadOnlyCollection<Alert> alerts, CancellationToken cancellationToken = default);
}
