namespace Danatadbir.Domain.Rules;

public record RuleEvaluation(EvaluationOutcome Outcome, string? Reason = null)
{
    public bool IsViolation => Outcome == EvaluationOutcome.Violated;

    public static RuleEvaluation NotApplicable() => new(EvaluationOutcome.NotApplicable);

    public static RuleEvaluation Satisfied() => new(EvaluationOutcome.Satisfied);

    public static RuleEvaluation Violated(string reason) => new(EvaluationOutcome.Violated, reason);
}
