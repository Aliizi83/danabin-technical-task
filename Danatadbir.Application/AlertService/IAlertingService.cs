using Danatadbir.Application.AlertService.Dtos;
using Danatadbir.Application.RuleService.Dtos;

namespace Danatadbir.Application.AlertService;

public interface IAlertingService
{
    /// <summary>
    /// Turns sustained episodes into alerts, applying each rule's cooldown, and stores the new ones.
    /// Processing the same episodes again stores nothing further.
    /// </summary>
    Task<AlertingResultDto> ProcessAsync(
        IReadOnlyList<RuleEpisodeDto> episodes,
        CancellationToken cancellationToken = default);
}
