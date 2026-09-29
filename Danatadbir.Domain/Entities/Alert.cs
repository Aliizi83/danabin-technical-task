using Danatadbir.Domain.Entities.Common;

namespace Danatadbir.Domain.Entities;
public class Alert : BaseEntity
{
    public required string RuleId { get; set; }

    public required string RuleName { get; set; }

    public required string SensorExternalId { get; set; }

    public required string MetricKey { get; set; }

    /// <summary>When the sustained condition began.</summary>
    public required DateTime StartTs { get; set; }

    /// <summary>When it ended, or the last observed reading if it was still holding.</summary>
    public required DateTime EndTs { get; set; }

    /// <summary>Most extreme value observed inside the episode.</summary>
    public required double PeakValue { get; set; }

    /// <summary>How many readings fell inside the episode.</summary>
    public required int ReadingCount { get; set; }

    public TimeSpan Duration => EndTs - StartTs;
}
