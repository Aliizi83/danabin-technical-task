using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Rules;

public abstract class SeriesRuleOperator<TParameters> : ISeriesRuleOperator
    where TParameters : IOperatorParameters
{
    public abstract string Key { get; }

    public Type ParametersType => typeof(TParameters);

    public IReadOnlyList<ViolationEpisode> Detect(IReadOnlyList<SensorData> series, IOperatorParameters parameters)
    {
        if (parameters is not TParameters typed)
        {
            throw new ArgumentException(
                $"Operator '{Key}' expects {typeof(TParameters).Name} but received {parameters.GetType().Name}.",
                nameof(parameters));
        }

        return Detect(series, typed);
    }

    protected abstract IReadOnlyList<ViolationEpisode> Detect(IReadOnlyList<SensorData> series, TParameters parameters);
}
