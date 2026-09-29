namespace Danatadbir.Application.AlertService.Dtos;

public record AlertDto(
    string RuleId,
    string RuleName,
    string SensorExternalId,
    string MetricKey,
    DateTime StartTs,
    DateTime EndTs,
    double PeakValue,
    int ReadingCount);
