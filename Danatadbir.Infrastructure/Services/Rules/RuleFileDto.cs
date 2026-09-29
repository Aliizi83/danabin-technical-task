using System.Text.Json;
using System.Text.Json.Serialization;

namespace Danatadbir.Infrastructure.Services.Rules;

public class RuleFileDto
{
    [JsonPropertyName("rules")]
    public List<RuleDefinitionDto> Rules { get; set; } = [];
}


public class RuleDefinitionDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("metric")]
    public string? Metric { get; set; }

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("alertCooldownSeconds")]
    public int? AlertCooldownSeconds { get; set; }

    [JsonPropertyName("operatorParameters")]
    public JsonElement? OperatorParameters { get; set; }
}
