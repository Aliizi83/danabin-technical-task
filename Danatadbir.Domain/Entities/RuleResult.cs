using Danatadbir.Domain.Entities.Common;

namespace Danatadbir.Domain.Entities;

public class RuleResult : BaseEntity
{
    public required string SensorExternalId { get; set; }

    public required string MetricKey { get; set; }

    public required DateTime Timestamp { get; set; }

    public required long Seq { get; set; }

    public required string RuleId { get; set; }

    public required string RuleName { get; set; }

    public required string Reason { get; set; }

    public required double Value { get; set; }
}
