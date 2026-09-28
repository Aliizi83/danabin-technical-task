using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules.Parameters;

namespace Danatadbir.Domain.Rules.Operators;

public class GreaterThanOperator : ReadingRuleOperator<ThresholdParameters>
{
    public override string Key => "GreaterThan";

    protected override RuleEvaluation Evaluate(SensorData reading, ThresholdParameters parameters) =>
        reading.Value > parameters.Value
            ? RuleEvaluation.Violated($"value {reading.Value} is above the threshold {parameters.Value}")
            : RuleEvaluation.Satisfied();
}
