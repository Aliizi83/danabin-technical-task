using Danatadbir.Domain.Rules;
using Danatadbir.Domain.Rules.Operators;
using Danatadbir.Domain.Rules.Parameters;
using Danatadbir.Tests.Fakes;

namespace Danatadbir.Tests.Domain;

public class OperatorTests
{
    private static bool Fires(IReadingRuleOperator op, IOperatorParameters parameters, double value) =>
        op.Evaluate(TestData.Reading(0, value), parameters).IsViolation;

    private static ThresholdParameters Threshold(double value) => new() { Threshold = value };

    // A rule states the violation condition, so every operator fires when its condition holds.

    [Theory]
    [InlineData(90.001, true)]
    [InlineData(90.0, false)]
    [InlineData(89.0, false)]
    public void GreaterThan_is_strict(double value, bool fires) =>
        Assert.Equal(fires, Fires(new GreaterThanOperator(), Threshold(90), value));

    [Theory]
    [InlineData(14.0, true)]
    [InlineData(13.999, false)]
    [InlineData(20.0, true)]
    public void GreaterThanOrEqual_includes_the_bound(double value, bool fires) =>
        Assert.Equal(fires, Fires(new GreaterThanOrEqualOperator(), Threshold(14), value));

    [Theory]
    [InlineData(-0.001, true)]
    [InlineData(0.0, false)]
    [InlineData(1.0, false)]
    public void LessThan_is_strict(double value, bool fires) =>
        Assert.Equal(fires, Fires(new LessThanOperator(), Threshold(0), value));

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(-3.2, true)]
    [InlineData(0.001, false)]
    public void LessThanOrEqual_includes_the_bound(double value, bool fires) =>
        Assert.Equal(fires, Fires(new LessThanOrEqualOperator(), Threshold(0), value));

    [Theory]
    [InlineData(-9999.0, true)]
    [InlineData(-9999.0009, true)]
    [InlineData(-9998.99, false)]
    [InlineData(0.0, false)]
    public void Equal_matches_within_tolerance(double value, bool fires) =>
        Assert.Equal(fires, Fires(new EqualOperator(), new EqualParameters { Value = -9999, Tolerance = 0.001 }, value));

    [Theory]
    [InlineData(10.5, true)]
    [InlineData(9.5, true)]
    [InlineData(10.5001, false)]
    public void Equal_includes_a_difference_of_exactly_the_tolerance(double value, bool fires) =>
        Assert.Equal(fires, Fires(new EqualOperator(), new EqualParameters { Value = 10, Tolerance = 0.5 }, value));

    [Theory]
    [InlineData(2.8, true)]
    [InlineData(3.0, true)]
    [InlineData(2.9, true)]
    [InlineData(2.799, false)]
    [InlineData(3.001, false)]
    public void Between_includes_both_bounds(double value, bool fires) =>
        Assert.Equal(fires, Fires(new BetweenOperator(), new BetweenParameters { Min = 2.8, Max = 3.0 }, value));

    [Fact]
    public void A_violation_carries_a_reason_that_names_the_value()
    {
        var result = new GreaterThanOperator().Evaluate(TestData.Reading(0, 95.5), Threshold(90));

        Assert.Equal(EvaluationOutcome.Violated, result.Outcome);
        Assert.Contains("95.5", result.Reason);
    }

    [Fact]
    public void A_satisfied_rule_has_no_reason()
    {
        var result = new GreaterThanOperator().Evaluate(TestData.Reading(0, 10), Threshold(90));

        Assert.Equal(EvaluationOutcome.Satisfied, result.Outcome);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void An_operator_rejects_parameters_of_another_operator()
    {
        Assert.Throws<ArgumentException>(() =>
            new BetweenOperator().Evaluate(TestData.Reading(0, 1), Threshold(1)));
    }

    [Fact]
    public void Threshold_parameters_require_a_finite_threshold()
    {
        Assert.NotEmpty(new ThresholdParameters().Validate());
        Assert.NotEmpty(new ThresholdParameters { Threshold = double.NaN }.Validate());
        Assert.Empty(new ThresholdParameters { Threshold = 1 }.Validate());
    }

    [Fact]
    public void Between_parameters_reject_an_inverted_band_and_missing_bounds()
    {
        Assert.NotEmpty(new BetweenParameters { Min = 5, Max = 1 }.Validate());
        Assert.NotEmpty(new BetweenParameters { Min = 5 }.Validate());
        Assert.Empty(new BetweenParameters { Min = 1, Max = 5 }.Validate());
        Assert.Empty(new BetweenParameters { Min = 3, Max = 3 }.Validate());
    }

    [Fact]
    public void Equal_parameters_reject_a_negative_tolerance()
    {
        Assert.NotEmpty(new EqualParameters { Value = 1, Tolerance = -0.1 }.Validate());
        Assert.NotEmpty(new EqualParameters { Tolerance = 0.1 }.Validate());
        Assert.Empty(new EqualParameters { Value = 1 }.Validate());
    }

    [Fact]
    public void SustainedAbove_parameters_require_a_positive_duration_and_gap()
    {
        Assert.NotEmpty(new SustainedAboveParameters { Threshold = 1, DurationSeconds = 0 }.Validate());
        Assert.NotEmpty(new SustainedAboveParameters { Threshold = 1, DurationSeconds = 10, MaxGapSeconds = 0 }.Validate());
        Assert.NotEmpty(new SustainedAboveParameters { DurationSeconds = 10 }.Validate());
        Assert.Empty(new SustainedAboveParameters { Threshold = 1, DurationSeconds = 10, MaxGapSeconds = 30 }.Validate());
    }
}
