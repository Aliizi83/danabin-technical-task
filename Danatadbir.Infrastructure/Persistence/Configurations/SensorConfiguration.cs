using Danatadbir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Danatadbir.Infrastructure.Persistence.Configurations;

public class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("sensors");

        builder.Property(sensor => sensor.ExternalId).HasMaxLength(64).IsRequired();
        builder.Property(sensor => sensor.Title).HasMaxLength(128).IsRequired();

        builder.HasIndex(sensor => sensor.ExternalId).IsUnique();

        builder.HasData(SeedData.Sensors);
    }
}
