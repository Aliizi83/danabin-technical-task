using System.Text.Json;
using Danatadbir.Domain.Rules;

namespace Danatadbir.Infrastructure.Services.Rules;

internal static class RuleDefinitionBinder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static BindResult Bind(RuleDefinitionDto definition, IReadOnlyDictionary<string, IRuleOperator> operators)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
            return BindResult.Failed("id is missing");

        if (string.IsNullOrWhiteSpace(definition.Name))
            return BindResult.Failed("name is missing");

        if (string.IsNullOrWhiteSpace(definition.Metric))
            return BindResult.Failed("metric is missing");

        if (string.IsNullOrWhiteSpace(definition.Operator))
            return BindResult.Failed("operator is missing");

        if (!operators.TryGetValue(definition.Operator, out var ruleOperator))
            return BindResult.Failed($"operator '{definition.Operator}' is not registered");

        if (definition.OperatorParameters is null)
            return BindResult.Failed($"operatorParameters is missing for operator '{definition.Operator}'");

        IOperatorParameters? parameters;

        try
        {
            parameters = (IOperatorParameters?)definition.OperatorParameters.Value.Deserialize(
                ruleOperator.ParametersType, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return BindResult.Failed(
                $"operatorParameters do not fit operator '{definition.Operator}': {exception.Message}");
        }

        if (parameters is null)
            return BindResult.Failed($"operatorParameters could not be read for operator '{definition.Operator}'");

        var errors = parameters.Validate();

        if (errors.Count > 0)
            return BindResult.Failed($"operatorParameters are invalid: {string.Join("; ", errors)}");

        return BindResult.Succeeded(new Rule
        {
            Id = definition.Id.Trim(),
            Name = definition.Name.Trim(),
            Metric = definition.Metric.Trim().ToLowerInvariant(),
            DeviceId = string.IsNullOrWhiteSpace(definition.DeviceId) ? null : definition.DeviceId.Trim(),
            Operator = ruleOperator,
            Enabled = definition.Enabled,
            Parameters = parameters
        });
    }

    public readonly record struct BindResult(Rule? Rule, string? Error)
    {
        public static BindResult Succeeded(Rule rule) => new(rule, null);

        public static BindResult Failed(string error) => new(null, error);
    }
}
