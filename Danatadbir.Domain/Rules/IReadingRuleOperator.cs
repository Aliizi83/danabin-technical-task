using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Rules;

public interface IReadingRuleOperator : IRuleOperator
{
    RuleEvaluation Evaluate(SensorData reading, IOperatorParameters parameters);
}
