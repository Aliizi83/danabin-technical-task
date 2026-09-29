using Danatadbir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Danatadbir.Infrastructure.Persistence.Configurations;

public class AlertConfigurations : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.Property(alert => alert.RuleId).HasMaxLength(128).IsRequired();
        builder.Property(alert => alert.RuleName).HasMaxLength(256).IsRequired();
        builder.Property(alert => alert.SensorExternalId).HasMaxLength(64).IsRequired();
        builder.Property(alert => alert.MetricKey).HasMaxLength(64).IsRequired();

        builder.Ignore(alert => alert.Duration);

        builder
            .HasIndex(alert => new
            {
                alert.RuleId,
                alert.SensorExternalId,
                alert.MetricKey,
                alert.StartTs
            })
            .IsUnique()
            .HasDatabaseName("ix_alerts_rule_series_start");

        builder
            .HasIndex(alert => new { alert.SensorExternalId, alert.MetricKey, alert.StartTs })
            .HasDatabaseName("ix_alerts_series_start");
    }
}
