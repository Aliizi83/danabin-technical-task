using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules.Parameters;

namespace Danatadbir.Domain.Rules.Operators;

public class EqualOperator : ReadingRuleOperator<EqualParameters>
{
    public override string Key => "Equal";

    protected override RuleEvaluation Evaluate(SensorData reading, EqualParameters parameters) =>
        Math.Abs(reading.Value - parameters.Target) <= parameters.Tolerance
            ? RuleEvaluation.Violated($"value {reading.Value} matches the fault value {parameters.Target}")
            : RuleEvaluation.Satisfied();
}
