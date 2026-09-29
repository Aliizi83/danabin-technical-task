using Danatadbir.Domain.Entities;

namespace Danatadbir.Application.RuleService.Dtos;

public record ReadingEvaluationDto(
    SensorData Reading,
    int EvaluationsPerformed,
    IReadOnlyList<RuleViolationDto> Violations)
{
    public bool IsAcceptable => Violations.Count == 0;
}
