using Danatadbir.Domain.Rules;
using Danatadbir.Domain.Rules.Operators;
using Danatadbir.Domain.Rules.Parameters;

namespace Danatadbir.Tests.Rules;

public class RuleApplicabilityTests
{
    private static Rule RuleFor(string metric, string? deviceId = null, bool enabled = true) => new()
    {
        Id = "r", Name = "r", Metric = metric, DeviceId = deviceId, Enabled = enabled,
        Operator = new GreaterThanOperator(), Parameters = new ThresholdParameters { Threshold = 1 }
    };

    [Fact]
    public void A_rule_without_a_device_applies_to_any_sensor_reporting_the_metric()
    {
        var rule = RuleFor("temperature");

        Assert.True(rule.AppliesTo("PUMP-01", "temperature"));
        Assert.True(rule.AppliesTo("FAN-03", "temperature"));
        Assert.False(rule.AppliesTo("PUMP-01", "pressure"));
    }

    [Fact]
    public void A_rule_with_a_device_applies_to_that_sensor_only()
    {
        var rule = RuleFor("temperature", "PUMP-01");

        Assert.True(rule.AppliesTo("PUMP-01", "temperature"));
        Assert.False(rule.AppliesTo("PUMP-02", "temperature"));
    }

    [Fact]
    public void A_disabled_rule_applies_to_nothing() =>
        Assert.False(RuleFor("temperature", enabled: false).AppliesTo("PUMP-01", "temperature"));

    [Fact]
    public void Matching_ignores_case() =>
        Assert.True(RuleFor("temperature", "PUMP-01").AppliesTo("pump-01", "TEMPERATURE"));
}
