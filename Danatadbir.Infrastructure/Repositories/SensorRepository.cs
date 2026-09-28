using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;
using Danatadbir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Danatadbir.Infrastructure.Repositories;

public class SensorRepository(AppDbContext context) : ISensorRepository
{
    public Task<List<Sensor>> GetAllAsync(CancellationToken cancellationToken = default) =>
        context.Sensors.AsNoTracking().ToListAsync(cancellationToken);
}
