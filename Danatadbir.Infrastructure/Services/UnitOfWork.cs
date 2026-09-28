using Danatadbir.Application.Common;
using Danatadbir.Infrastructure.Persistence;

namespace Danatadbir.Infrastructure.Services;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
