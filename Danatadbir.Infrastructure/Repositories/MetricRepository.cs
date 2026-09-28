using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;
using Danatadbir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Danatadbir.Infrastructure.Repositories;

public class MetricRepository(AppDbContext context) : IMetricRepository
{
    public Task<List<Metric>> GetAllAsync(CancellationToken cancellationToken = default) =>
        context.Metrics.AsNoTracking().ToListAsync(cancellationToken);
}
