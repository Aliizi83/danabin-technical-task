using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Rules;

public class Rule
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Metric { get; init; }

    public string? DeviceId { get; init; }

    public required IRuleOperator Operator { get; init; }

    public string OperatorKey => Operator.Key;

    public bool Enabled { get; init; } = true;

    /// <summary>Cooldown used when a stateful rule does not set its own; the task's default of 5 minutes.</summary>
    public static readonly TimeSpan DefaultAlertCooldown = TimeSpan.FromMinutes(5);

    /// <summary>
    /// After an alert for this rule on a given sensor, no other alert for the same sensor and metric
    /// is raised within this window. It belongs to the rule, not the operator: the operator only
    /// finds episodes, whereas deduplicating alerts is a policy over their stream.
    /// </summary>
    public TimeSpan AlertCooldown { get; init; } = DefaultAlertCooldown;

    public required IOperatorParameters Parameters { get; init; }

    public bool AppliesTo(SensorData reading) =>
        Enabled
        && string.Equals(Metric, reading.MetricKey, StringComparison.OrdinalIgnoreCase)
        && (DeviceId is null
            || string.Equals(DeviceId, reading.SensorExternalId, StringComparison.OrdinalIgnoreCase));
}
