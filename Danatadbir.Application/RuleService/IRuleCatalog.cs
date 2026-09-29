using Danatadbir.Domain.Rules;

namespace Danatadbir.Application.RuleService;
public interface IRuleCatalog
{
    IReadOnlyList<Rule> Rules { get; }

    IReadOnlyList<RejectedRule> RejectedRules { get; }

    IReadOnlyList<Rule> RulesFor(string sensorExternalId, string metricKey);
}

public record RejectedRule(string RuleId, string Reason);
