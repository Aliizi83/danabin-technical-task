using Danatadbir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Danatadbir.Infrastructure.Persistence.Configurations;

public class MetricConfiguration : IEntityTypeConfiguration<Metric>
{
    public void Configure(EntityTypeBuilder<Metric> builder)
    {
        builder.ToTable("metrics");

        builder.Property(metric => metric.Key).HasMaxLength(64).IsRequired();
        builder.Property(metric => metric.Title).HasMaxLength(128).IsRequired();
        builder.Property(metric => metric.Unit).HasMaxLength(32);

        builder.HasIndex(metric => metric.Key).IsUnique();

        builder.HasData(SeedData.Metrics);
    }
}
