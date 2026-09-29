using Danatadbir.Application.RuleService;
using Danatadbir.Application.RuleService.Dtos;
using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules;

namespace Danatadbir.Infrastructure.Services.Rules;

public class RuleEvaluationService(IRuleCatalog catalog) : IRuleEvaluationService
{
    public ReadingEvaluationDto Evaluate(SensorData reading)
    {
        var applicable = catalog.RulesFor(reading.SensorExternalId, reading.MetricKey);
        var violations = new List<RuleViolationDto>();
        var evaluations = 0;

        foreach (var rule in applicable)
        {
            if (rule.Operator is not IReadingRuleOperator readingOperator)
                continue;

            evaluations++;
            var evaluation = readingOperator.Evaluate(reading, rule.Parameters);

            if (evaluation.IsViolation)
                violations.Add(new RuleViolationDto(rule.Id, rule.Name, evaluation.Reason!));
        }

        return new ReadingEvaluationDto(reading, evaluations, violations);
    }
}
