namespace Danatadbir.Application.IngestionService.Dtos;

public record EpisodeSampleDto(
    string RuleId,
    string SensorExternalId,
    string MetricKey,
    DateTime StartTs,
    DateTime EndTs,
    double DurationSeconds,
    double PeakValue,
    int ReadingCount);
