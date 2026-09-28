using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Repositories;

public interface ISensorRepository
{
    Task<List<Sensor>> GetAllAsync(CancellationToken cancellationToken = default);
}
