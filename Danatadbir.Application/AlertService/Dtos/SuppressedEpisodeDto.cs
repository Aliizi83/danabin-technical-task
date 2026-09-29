namespace Danatadbir.Application.AlertService.Dtos;

/// <param name="SuppressedByStart">Start of the alert whose cooldown covered this episode.</param>
public record SuppressedEpisodeDto(
    string RuleId,
    string SensorExternalId,
    string MetricKey,
    DateTime StartTs,
    DateTime EndTs,
    DateTime SuppressedByStart);
