namespace Danatadbir.Application.IngestionService.Dtos;

public record ViolationSampleDto(
    string SensorExternalId,
    string MetricKey,
    DateTime Timestamp,
    double Value,
    string RuleId,
    string Reason);
