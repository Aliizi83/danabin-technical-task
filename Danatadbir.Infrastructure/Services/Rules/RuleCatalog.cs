using System.Text.Json;
using Danatadbir.Application.RuleService;
using Danatadbir.Domain.Rules;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Danatadbir.Infrastructure.Services.Rules;

public class RuleCatalog : IRuleCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly List<Rule> _rules = [];
    private readonly List<RejectedRule> _rejected = [];

    public RuleCatalog(
        IEnumerable<IRuleOperator> operators,
        IOptions<RuleCatalogOptions> options,
        IHostEnvironment environment,
        ILogger<RuleCatalog> logger)
    {
        var operatorsByKey = operators.ToDictionary(op => op.Key, StringComparer.OrdinalIgnoreCase);
        var path = ResolvePath(options.Value.FilePath, environment);

        if (!File.Exists(path))
        {
            logger.LogError("Rule file {Path} was not found; no rules are loaded", path);
            return;
        }

        RuleFileDto? file;

        try
        {
            file = JsonSerializer.Deserialize<RuleFileDto>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Rule file {Path} is not valid JSON; no rules are loaded", path);
            return;
        }

        foreach (var definition in file?.Rules ?? [])
        {
            var result = RuleDefinitionBinder.Bind(definition, operatorsByKey);

            if (result.Rule is not null)
            {
                _rules.Add(result.Rule);
                continue;
            }

            _rejected.Add(new RejectedRule(definition.Id ?? "<missing id>", result.Error!));
            logger.LogError("Rule {RuleId} was rejected: {Reason}", definition.Id ?? "<missing id>", result.Error);
        }

        logger.LogInformation(
            "Loaded {Loaded} rules from {Path} ({Enabled} enabled, {Disabled} disabled, {Rejected} rejected)",
            _rules.Count, path, _rules.Count(rule => rule.Enabled), _rules.Count(rule => !rule.Enabled),
            _rejected.Count);
    }

    public IReadOnlyList<Rule> Rules => _rules;

    public IReadOnlyList<RejectedRule> RejectedRules => _rejected;

    public IReadOnlyList<Rule> RulesFor(string sensorExternalId, string metricKey) =>
        _rules
            .Where(rule =>
                rule.Enabled
                && string.Equals(rule.Metric, metricKey, StringComparison.OrdinalIgnoreCase)
                && (rule.DeviceId is null
                    || string.Equals(rule.DeviceId, sensorExternalId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private static string ResolvePath(string path, IHostEnvironment environment)
    {
        if (Path.IsPathRooted(path))
            return path;

        var fromContentRoot = Path.Combine(environment.ContentRootPath, path);

        if (File.Exists(fromContentRoot))
            return fromContentRoot;

        var parent = Directory.GetParent(environment.ContentRootPath)?.FullName;

        return parent is null ? fromContentRoot : Path.Combine(parent, path);
    }
}
