using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules.Parameters;

namespace Danatadbir.Domain.Rules.Operators;

public class GreaterThanOrEqualOperator : ReadingRuleOperator<ThresholdParameters>
{
    public override string Key => "GreaterThanOrEqual";

    protected override RuleEvaluation Evaluate(SensorData reading, ThresholdParameters parameters) =>
        reading.Value >= parameters.Value
            ? RuleEvaluation.Violated($"value {reading.Value} reached the threshold {parameters.Value}")
            : RuleEvaluation.Satisfied();
}
