using System.Globalization;
using System.Text.Json;
using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Domain.Entities;

namespace Danatadbir.Infrastructure.Services.Ingestion;

public static class ReadingLineParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // Only ISO-8601 is accepted. A lenient parser would read "08:33:00" as today's date and
    // "03/01/2030" as an American date, so the result would depend on the machine, not the feed.
    // A timestamp without an offset is taken as UTC, which is what the feed is documented to be.
    private static readonly string[] TimestampFormats =
        ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];

    private static bool TryParseTimestamp(string text, out DateTime utc)
    {
        var parsed = DateTimeOffset.TryParseExact(
            text.Trim(),
            TimestampFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var result);

        utc = result.UtcDateTime;
        return parsed;
    }

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

        if (!TryParseTimestamp(dto.Timestamp, out var timestamp))
        {
            return ParseOutcome.Rejected(RejectionReason.InvalidField, $"ts '{dto.Timestamp}' is not a valid ISO-8601 timestamp");
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
            timestamp,
            dto.Value.Value,
            dto.Seq.Value);

        return ParseOutcome.Parsed(reading);
    }
}
