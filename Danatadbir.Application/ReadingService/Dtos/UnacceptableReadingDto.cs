namespace Danatadbir.Application.ReadingService.Dtos;

public record ViolatedRuleDto(string RuleId, string RuleName, string Reason);

public record UnacceptableReadingDto(DateTime Timestamp, double Value, long Seq, IReadOnlyList<ViolatedRuleDto> Violations);
