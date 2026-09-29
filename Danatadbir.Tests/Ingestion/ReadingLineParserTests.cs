using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Infrastructure.Services.Ingestion;

namespace Danatadbir.Tests.Ingestion;

public class ReadingLineParserTests
{
    private static string Line(string ts = "2025-06-01T08:33:00Z", string device = "\"PUMP-01\"", string metric = "\"temperature\"",
        string value = "67.21", string seq = "1199") =>
        $$"""{"deviceId":{{device}},"metric":{{metric}},"ts":"{{ts}}","value":{{value}},"seq":{{seq}}}""";

    [Fact]
    public void A_valid_line_is_parsed()
    {
        var outcome = ReadingLineParser.Parse(Line());

        Assert.True(outcome.IsParsed);
        Assert.Equal("PUMP-01", outcome.Reading!.SensorExternalId);
        Assert.Equal("temperature", outcome.Reading.MetricKey);
        Assert.Equal(new DateTime(2025, 6, 1, 8, 33, 0, DateTimeKind.Utc), outcome.Reading.Timestamp);
        Assert.Equal(67.21, outcome.Reading.Value);
        Assert.Equal(1199, outcome.Reading.Seq);
    }

    [Theory]
    [InlineData("{bad json")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:33:00Z","value":"abc","seq":1}""")]
    [InlineData("""{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:33:00Z","value":1,"seq":"x"}""")]
    [InlineData("null")]
    public void Unreadable_lines_are_malformed_and_never_throw(string line)
    {
        var outcome = ReadingLineParser.Parse(line);

        Assert.False(outcome.IsParsed);
        Assert.Equal(RejectionReason.Malformed, outcome.Reason);
    }

    [Theory]
    [InlineData("""{"deviceId":null,"metric":"temperature","ts":"2025-06-01T08:33:00Z","value":1,"seq":1}""", "deviceId")]
    [InlineData("""{"deviceId":"","metric":"temperature","ts":"2025-06-01T08:33:00Z","value":1,"seq":1}""", "deviceId")]
    [InlineData("""{"deviceId":"   ","metric":"temperature","ts":"2025-06-01T08:33:00Z","value":1,"seq":1}""", "deviceId")]
    [InlineData("""{"deviceId":"PUMP-01","metric":null,"ts":"2025-06-01T08:33:00Z","value":1,"seq":1}""", "metric")]
    [InlineData("""{"deviceId":"PUMP-01","metric":"temperature","value":1,"seq":1}""", "ts")]
    [InlineData("""{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:33:00Z","seq":1}""", "value")]
    [InlineData("""{"deviceId":"PUMP-01","metric":"temperature","ts":"2025-06-01T08:33:00Z","value":1}""", "seq")]
    [InlineData("{}", "deviceId")]
    public void A_missing_or_empty_field_is_invalid_and_named(string line, string field)
    {
        var outcome = ReadingLineParser.Parse(line);

        Assert.Equal(RejectionReason.InvalidField, outcome.Reason);
        Assert.Contains(field, outcome.Detail);
    }

    [Theory]
    [InlineData("2025-06-31T08:04:10Z")]
    [InlineData("2025-02-30T00:00:00Z")]
    [InlineData("2025-06-01T25:00:00Z")]
    [InlineData("08:33:00")]
    [InlineData("2025-06-01 08:33:00")]
    [InlineData("06/01/2025 08:33:00")]
    [InlineData("2025-06-01T08:33Z")]
    [InlineData("yesterday")]
    [InlineData("")]
    public void Anything_that_is_not_a_valid_iso_8601_timestamp_is_invalid(string ts)
    {
        var outcome = ReadingLineParser.Parse(Line(ts: ts));

        Assert.False(outcome.IsParsed);
        Assert.Equal(RejectionReason.InvalidField, outcome.Reason);
    }

    [Theory]
    [InlineData("2025-06-01T08:33:00Z", "2025-06-01T08:33:00")]
    [InlineData("2025-06-01T08:33:00", "2025-06-01T08:33:00")]
    [InlineData("2025-06-01T12:03:00+03:30", "2025-06-01T08:33:00")]
    [InlineData("2025-06-01T05:33:00-03:00", "2025-06-01T08:33:00")]
    public void Timestamps_are_normalised_to_utc_and_a_missing_offset_means_utc(string ts, string expectedUtc)
    {
        var reading = ReadingLineParser.Parse(Line(ts: ts)).Reading!;

        Assert.Equal(DateTime.Parse(expectedUtc, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal), reading.Timestamp);
        Assert.Equal(DateTimeKind.Utc, reading.Timestamp.Kind);
    }

    [Fact]
    public void Fractional_seconds_are_kept()
    {
        var reading = ReadingLineParser.Parse(Line(ts: "2025-06-01T08:33:00.250Z")).Reading!;

        Assert.Equal(250, reading.Timestamp.Millisecond);
    }

    [Fact]
    public void Identifiers_are_trimmed_and_the_metric_is_lowercased()
    {
        var reading = ReadingLineParser.Parse(Line(device: "\" PUMP-01 \"", metric: "\"Temperature\"")).Reading!;

        Assert.Equal("PUMP-01", reading.SensorExternalId);
        Assert.Equal("temperature", reading.MetricKey);
    }

    [Theory]
    [InlineData("1000000")]
    [InlineData("-9999")]
    [InlineData("1e308")]
    [InlineData("0")]
    [InlineData("-0.0")]
    public void Any_finite_number_is_a_valid_value_because_plausibility_belongs_to_the_rules(string value) =>
        Assert.True(ReadingLineParser.Parse(Line(value: value)).IsParsed);
}
