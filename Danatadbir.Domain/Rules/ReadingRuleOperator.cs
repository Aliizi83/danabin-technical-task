using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Rules;

public abstract class ReadingRuleOperator<TParameters> : IReadingRuleOperator
    where TParameters : IOperatorParameters
{
    public abstract string Key { get; }

    public Type ParametersType => typeof(TParameters);

    public RuleEvaluation Evaluate(SensorData reading, IOperatorParameters parameters)
    {
        if (parameters is not TParameters typed)
        {
            throw new ArgumentException(
                $"Operator '{Key}' expects {typeof(TParameters).Name} but received {parameters.GetType().Name}.",
                nameof(parameters));
        }

        return Evaluate(reading, typed);
    }

    protected abstract RuleEvaluation Evaluate(SensorData reading, TParameters parameters);
}
