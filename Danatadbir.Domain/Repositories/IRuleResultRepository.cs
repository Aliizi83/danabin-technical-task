using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Repositories;

public interface IRuleResultRepository
{
    Task<int> AddMissingAsync(IReadOnlyCollection<RuleResult> results, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
