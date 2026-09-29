namespace Danatadbir.Application.AlertService.Dtos;

/// <param name="AlertsGenerated">Alerts this input gives rise to, including ones stored by an earlier run.</param>
/// <param name="AlertsStored">Alerts newly written by this run; zero when the same input is processed again.</param>
/// <param name="EpisodesSuppressed">Episodes detected but covered by a cooldown, so no alert was raised.</param>
public record AlertingResultDto(
    int AlertsGenerated,
    int AlertsStored,
    int EpisodesSuppressed,
    IReadOnlyList<AlertDto> Alerts,
    IReadOnlyList<SuppressedEpisodeDto> Suppressed)
{
    public static AlertingResultDto Empty { get; } = new(0, 0, 0, [], []);
}
