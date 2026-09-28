using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Rules;

public class Rule
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Metric { get; init; }

    public string? DeviceId { get; init; }

    public required string Operator { get; init; }

    public bool Enabled { get; init; } = true;

    public required IOperatorParameters Parameters { get; init; }

    public bool AppliesTo(SensorData reading) =>
        Enabled
        && string.Equals(Metric, reading.MetricKey, StringComparison.OrdinalIgnoreCase)
        && (DeviceId is null
            || string.Equals(DeviceId, reading.SensorExternalId, StringComparison.OrdinalIgnoreCase));
}
