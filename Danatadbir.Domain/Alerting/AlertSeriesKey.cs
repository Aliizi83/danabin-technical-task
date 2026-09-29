namespace Danatadbir.Domain.Alerting;

/// <summary>The combination cooldown applies to: one rule on one sensor's metric.</summary>
public record AlertSeriesKey(string RuleId, string SensorExternalId, string MetricKey);
