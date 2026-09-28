using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Rules;

public interface ISeriesRuleOperator : IRuleOperator
{
    IReadOnlyList<ViolationEpisode> Detect(IReadOnlyList<SensorData> series, IOperatorParameters parameters);
}
