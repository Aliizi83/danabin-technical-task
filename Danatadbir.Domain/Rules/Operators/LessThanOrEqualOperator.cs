using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules.Parameters;

namespace Danatadbir.Domain.Rules.Operators;

public class LessThanOrEqualOperator : ReadingRuleOperator<ThresholdParameters>
{
    public override string Key => "LessThanOrEqual";

    protected override RuleEvaluation Evaluate(SensorData reading, ThresholdParameters parameters) =>
        reading.Value <= parameters.Value
            ? RuleEvaluation.Violated($"value {reading.Value} dropped to the threshold {parameters.Value}")
            : RuleEvaluation.Satisfied();
}
