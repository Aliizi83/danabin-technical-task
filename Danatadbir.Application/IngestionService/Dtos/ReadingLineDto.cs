using System.Text.Json.Serialization;

namespace Danatadbir.Application.IngestionService.Dtos;

public class ReadingLineDto
{
    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("metric")]
    public string? Metric { get; set; }

    [JsonPropertyName("ts")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("value")]
    public double? Value { get; set; }

    [JsonPropertyName("seq")]
    public long? Seq { get; set; }
}
