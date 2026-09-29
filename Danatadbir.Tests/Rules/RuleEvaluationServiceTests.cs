using Danatadbir.Infrastructure.Services.Rules;
using Danatadbir.Tests.Fakes;

namespace Danatadbir.Tests.Rules;

public class RuleEvaluationServiceTests
{
    private const string Over90 = """{"threshold":90}""";
    private const string Sustained = """{"threshold":70,"durationSeconds":60}""";

    private static RuleEvaluationService Service(params string[] rules) => new(TestCatalog.FromRules(rules));

    [Fact]
    public void A_rule_without_a_device_applies_to_every_sensor_with_the_metric()
    {
        var service = Service(TestCatalog.Rule("fleet", "temperature", "GreaterThan", Over90));

        Assert.Equal(1, service.Evaluate(TestData.Reading(0, 50, sensor: "PUMP-01")).EvaluationsPerformed);
        Assert.Equal(1, service.Evaluate(TestData.Reading(0, 50, sensor: "COMP-01")).EvaluationsPerformed);
    }

    [Fact]
    public void A_rule_with_a_device_applies_to_that_sensor_only()
    {
        var service = Service(TestCatalog.Rule("mine", "temperature", "GreaterThan", Over90, deviceId: "PUMP-01"));

        Assert.Equal(1, service.Evaluate(TestData.Reading(0, 50, sensor: "PUMP-01")).EvaluationsPerformed);
        Assert.Equal(0, service.Evaluate(TestData.Reading(0, 50, sensor: "PUMP-02")).EvaluationsPerformed);
    }

    [Fact]
    public void A_rule_applies_to_its_metric_only()
    {
        var service = Service(TestCatalog.Rule("t", "temperature", "GreaterThan", Over90));

        Assert.Equal(0, service.Evaluate(TestData.Reading(0, 500, metric: "pressure")).EvaluationsPerformed);
    }

    [Fact]
    public void A_disabled_rule_is_not_evaluated_even_when_it_would_fire()
    {
        var service = Service(TestCatalog.Rule("off", "temperature", "GreaterThan", Over90, enabled: false));

        var result = service.Evaluate(TestData.Reading(0, 500));

        Assert.Equal(0, result.EvaluationsPerformed);
        Assert.True(result.IsAcceptable);
    }

    [Fact]
    public void A_reading_is_acceptable_when_it_satisfies_every_applicable_rule()
    {
        var service = Service(
            TestCatalog.Rule("a", "temperature", "GreaterThan", Over90),
            TestCatalog.Rule("b", "temperature", "LessThan", """{"threshold":0}"""));

        var result = service.Evaluate(TestData.Reading(0, 50));

        Assert.True(result.IsAcceptable);
        Assert.Equal(2, result.EvaluationsPerformed);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void One_violated_rule_is_enough_to_make_a_reading_unacceptable()
    {
        var service = Service(
            TestCatalog.Rule("fine", "temperature", "LessThan", """{"threshold":0}"""),
            TestCatalog.Rule("broken", "temperature", "GreaterThan", Over90));

        var result = service.Evaluate(TestData.Reading(0, 95));

        Assert.False(result.IsAcceptable);
        var violation = Assert.Single(result.Violations);
        Assert.Equal("broken", violation.RuleId);
        Assert.False(string.IsNullOrWhiteSpace(violation.Reason));
    }

    [Fact]
    public void Every_violated_rule_is_reported()
    {
        var service = Service(
            TestCatalog.Rule("a", "temperature", "GreaterThan", """{"threshold":10}"""),
            TestCatalog.Rule("b", "temperature", "GreaterThanOrEqual", """{"threshold":20}"""));

        Assert.Equal(["a", "b"], service.Evaluate(TestData.Reading(0, 30)).Violations.Select(v => v.RuleId));
    }

    [Fact]
    public void A_reading_no_rule_applies_to_is_acceptable()
    {
        var result = Service().Evaluate(TestData.Reading(0, 1e9));

        Assert.True(result.IsAcceptable);
        Assert.Equal(0, result.EvaluationsPerformed);
    }

    [Fact]
    public void A_sustained_rule_gives_no_per_reading_verdict_and_is_not_counted()
    {
        var service = Service(TestCatalog.Rule("s", "temperature", "SustainedAbove", Sustained));

        var result = service.Evaluate(TestData.Reading(0, 1000));

        Assert.True(result.IsAcceptable);
        Assert.Equal(0, result.EvaluationsPerformed);
    }

    [Fact]
    public void An_absurd_but_finite_value_is_evaluated_not_rejected()
    {
        var service = Service(TestCatalog.Rule("t", "temperature", "GreaterThan", Over90));

        Assert.False(service.Evaluate(TestData.Reading(0, 1_000_000)).IsAcceptable);
    }
}
