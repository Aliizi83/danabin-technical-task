using Danatadbir.Domain.Entities;
using Danatadbir.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Danatadbir.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Sensor> Sensors { get; set; }
    public DbSet<Metric> Metrics { get; set; }
    public DbSet<RuleResult> RuleResults { get; set; }
    public DbSet<Alert> Alerts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplySoftDeleteFilter();
    }
}
