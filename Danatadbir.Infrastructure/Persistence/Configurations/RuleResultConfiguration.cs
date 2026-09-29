using Danatadbir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Danatadbir.Infrastructure.Persistence.Configurations;

public class RuleResultConfiguration : IEntityTypeConfiguration<RuleResult>
{
    public void Configure(EntityTypeBuilder<RuleResult> builder)
    {
        builder.ToTable("rule_results");

        builder.Property(result => result.SensorExternalId).HasMaxLength(64).IsRequired();
        builder.Property(result => result.MetricKey).HasMaxLength(64).IsRequired();
        builder.Property(result => result.RuleId).HasMaxLength(128).IsRequired();
        builder.Property(result => result.RuleName).HasMaxLength(256).IsRequired();
        builder.Property(result => result.Reason).HasMaxLength(512).IsRequired();

        // Natural key of a violation: one reading, one rule. Re-running the same feed hits this
        // index instead of inserting a second row.
        builder
            .HasIndex(result => new
            {
                result.SensorExternalId,
                result.MetricKey,
                result.Timestamp,
                result.Seq,
                result.RuleId
            })
            .IsUnique()
            .HasDatabaseName("ix_rule_results_reading_rule");

        builder.HasIndex(result => new { result.SensorExternalId, result.MetricKey, result.Timestamp })
            .HasDatabaseName("ix_rule_results_reading");
    }
}
