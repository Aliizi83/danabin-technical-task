using System.Globalization;
using System.Text.Json;
using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Domain.Entities;

namespace Danatadbir.Infrastructure.Services.Ingestion;

public static class ReadingLineParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ParseOutcome Parse(string line)
    {
        ReadingLineDto? dto;

        try
        {
            dto = JsonSerializer.Deserialize<ReadingLineDto>(line, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return ParseOutcome.Rejected(RejectionReason.Malformed, exception.Message);
        }

        if (dto is null)
            return ParseOutcome.Rejected(RejectionReason.Malformed, "line deserialized to null");

        if (string.IsNullOrWhiteSpace(dto.DeviceId))
            return ParseOutcome.Rejected(RejectionReason.InvalidField, "deviceId is missing or empty");

        if (string.IsNullOrWhiteSpace(dto.Metric))
            return ParseOutcome.Rejected(RejectionReason.InvalidField, "metric is missing or empty");

        if (string.IsNullOrWhiteSpace(dto.Timestamp))
            return ParseOutcome.Rejected(RejectionReason.InvalidField, "ts is missing or empty");

        if (!DateTime.TryParse(
                dto.Timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return ParseOutcome.Rejected(RejectionReason.InvalidField, $"ts '{dto.Timestamp}' is not a valid timestamp");
        }

        if (dto.Value is null)
            return ParseOutcome.Rejected(RejectionReason.InvalidField, "value is missing");

        if (double.IsNaN(dto.Value.Value) || double.IsInfinity(dto.Value.Value))
            return ParseOutcome.Rejected(RejectionReason.InvalidField, "value is not a finite number");

        if (dto.Seq is null)
            return ParseOutcome.Rejected(RejectionReason.InvalidField, "seq is missing");

        var reading = new SensorData(
            dto.DeviceId.Trim(),
            dto.Metric.Trim().ToLowerInvariant(),
            DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            dto.Value.Value,
            dto.Seq.Value);

        return ParseOutcome.Parsed(reading);
    }
}
