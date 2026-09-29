using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules.Parameters;

namespace Danatadbir.Domain.Rules.Operators;

public class BetweenOperator : ReadingRuleOperator<BetweenParameters>
{
    public override string Key => "Between";

    protected override RuleEvaluation Evaluate(SensorData reading, BetweenParameters parameters) =>
        reading.Value >= parameters.LowerBound && reading.Value <= parameters.UpperBound
            ? RuleEvaluation.Violated(
                $"value {reading.Value} is inside the band [{parameters.LowerBound}, {parameters.UpperBound}]")
            : RuleEvaluation.Satisfied();
}
