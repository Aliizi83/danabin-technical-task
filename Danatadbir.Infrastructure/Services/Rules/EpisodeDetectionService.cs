using Danatadbir.Application.RuleService;
using Danatadbir.Application.RuleService.Dtos;
using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules;

namespace Danatadbir.Infrastructure.Services.Rules;

public class EpisodeDetectionService(IRuleCatalog catalog) : IEpisodeDetectionService
{
    public IReadOnlyList<RuleEpisodeDto> Detect(
        string sensorExternalId,
        string metricKey,
        IReadOnlyList<SensorData> series)
    {
        var episodes = new List<RuleEpisodeDto>();

        if (series.Count == 0)
            return episodes;

        foreach (var rule in catalog.RulesFor(sensorExternalId, metricKey))
        {
            // A fleet-wide rule is evaluated once per series, so each sensor gets its own episodes.
            if (rule.Operator is not ISeriesRuleOperator seriesOperator)
                continue;

            foreach (var episode in seriesOperator.Detect(series, rule.Parameters))
                episodes.Add(new RuleEpisodeDto(rule.Id, rule.Name, sensorExternalId, metricKey, episode));
        }

        return episodes;
    }
}
