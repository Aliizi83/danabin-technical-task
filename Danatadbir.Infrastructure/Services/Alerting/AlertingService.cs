using Danatadbir.Application.AlertService;
using Danatadbir.Application.AlertService.Dtos;
using Danatadbir.Application.RuleService;
using Danatadbir.Application.RuleService.Dtos;
using Danatadbir.Domain.Alerting;
using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Danatadbir.Infrastructure.Services.Alerting;

public class AlertingService(
    IRuleCatalog ruleCatalog,
    IAlertRepository alertRepository,
    ILogger<AlertingService> logger) : IAlertingService
{
    public async Task<AlertingResultDto> ProcessAsync(
        IReadOnlyList<RuleEpisodeDto> episodes,
        CancellationToken cancellationToken = default)
    {
        if (episodes.Count == 0)
            return AlertingResultDto.Empty;

        var cooldowns = ruleCatalog.Rules.ToDictionary(rule => rule.Id, rule => rule.AlertCooldown);

        var candidates = episodes
            .Select(episode => new EpisodeCandidate(
                new AlertSeriesKey(episode.RuleId, episode.SensorExternalId, episode.MetricKey),
                episode.RuleName,
                cooldowns[episode.RuleId],
                episode.Episode))
            .ToList();

        var ruleIds = candidates.Select(candidate => candidate.Key.RuleId).Distinct().ToList();
        var existing = await alertRepository.GetAlertStartsAsync(ruleIds, cancellationToken);
        var plan = CooldownPlanner.Plan(candidates, existing);

        var generated = plan.Where(planned => planned.Decision != AlertDecision.Suppressed).ToList();
        var suppressed = plan.Where(planned => planned.Decision == AlertDecision.Suppressed).ToList();

        var stored = await alertRepository.AddMissingAsync(
            plan.Where(planned => planned.Decision == AlertDecision.Raise).Select(ToEntity).ToList(),
            cancellationToken);

        foreach (var planned in plan)
            Log(planned);

        return new AlertingResultDto(
            generated.Count,
            stored,
            suppressed.Count,
            generated.Select(planned => ToDto(planned.Candidate)).ToList(),
            suppressed.Select(planned => new SuppressedEpisodeDto(
                planned.Candidate.Key.RuleId,
                planned.Candidate.Key.SensorExternalId,
                planned.Candidate.Key.MetricKey,
                planned.Candidate.Episode.StartTs,
                planned.Candidate.Episode.EndTs,
                planned.SuppressedBy!.Value)).ToList());
    }

    private void Log(PlannedAlert planned)
    {
        var key = planned.Candidate.Key;
        var episode = planned.Candidate.Episode;

        switch (planned.Decision)
        {
            case AlertDecision.Raise:
                logger.LogWarning(
                    "Alert raised: rule {RuleId} on {Sensor}/{Metric} from {StartTs:o} to {EndTs:o}, peak {Peak}",
                    key.RuleId, key.SensorExternalId, key.MetricKey, episode.StartTs, episode.EndTs, episode.PeakValue);
                break;

            case AlertDecision.Suppressed:
                logger.LogInformation(
                    "Episode suppressed by cooldown: rule {RuleId} on {Sensor}/{Metric} from {StartTs:o}, "
                    + "within {Cooldown} of the alert at {AlertStart:o}",
                    key.RuleId, key.SensorExternalId, key.MetricKey, episode.StartTs,
                    planned.Candidate.Cooldown, planned.SuppressedBy);
                break;

            default:
                logger.LogDebug(
                    "Alert already raised: rule {RuleId} on {Sensor}/{Metric} from {StartTs:o}",
                    key.RuleId, key.SensorExternalId, key.MetricKey, episode.StartTs);
                break;
        }
    }

    private static Alert ToEntity(PlannedAlert planned) => new()
    {
        RuleId = planned.Candidate.Key.RuleId,
        RuleName = planned.Candidate.RuleName,
        SensorExternalId = planned.Candidate.Key.SensorExternalId,
        MetricKey = planned.Candidate.Key.MetricKey,
        StartTs = planned.Candidate.Episode.StartTs,
        EndTs = planned.Candidate.Episode.EndTs,
        PeakValue = planned.Candidate.Episode.PeakValue,
        ReadingCount = planned.Candidate.Episode.ReadingCount
    };

    private static AlertDto ToDto(EpisodeCandidate candidate) => new(
        candidate.Key.RuleId,
        candidate.RuleName,
        candidate.Key.SensorExternalId,
        candidate.Key.MetricKey,
        candidate.Episode.StartTs,
        candidate.Episode.EndTs,
        candidate.Episode.PeakValue,
        candidate.Episode.ReadingCount);
}
