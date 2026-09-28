using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules.Parameters;

namespace Danatadbir.Domain.Rules.Operators;

public class LessThanOperator : ReadingRuleOperator<ThresholdParameters>
{
    public override string Key => "LessThan";

    protected override RuleEvaluation Evaluate(SensorData reading, ThresholdParameters parameters) =>
        reading.Value < parameters.Value
            ? RuleEvaluation.Violated($"value {reading.Value} is below the threshold {parameters.Value}")
            : RuleEvaluation.Satisfied();
}
