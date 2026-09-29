using Danatadbir.Domain.Rules;

namespace Danatadbir.Application.RuleService.Dtos;

public record RuleEpisodeDto(
    string RuleId,
    string RuleName,
    string SensorExternalId,
    string MetricKey,
    ViolationEpisode Episode);
