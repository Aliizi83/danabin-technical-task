using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Repositories;

public interface IMetricRepository
{
    Task<List<Metric>> GetAllAsync(CancellationToken cancellationToken = default);
}
