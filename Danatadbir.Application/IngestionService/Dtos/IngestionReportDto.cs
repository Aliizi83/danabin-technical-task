namespace Danatadbir.Application.IngestionService.Dtos;

public record IngestionReportDto
{
    public required string Source { get; init; }
    public int TotalLinesRead { get; init; }
    public int ParsedReadings { get; init; }
    public int StoredReadings { get; init; }
    public int DuplicatesRemoved { get; init; }
    public int MalformedLines { get; init; }
    public int InvalidRecords { get; init; }
    public int UnknownSensorOrMetric { get; init; }
    public double DurationMs { get; init; }

    public IReadOnlyList<RejectedLineDto> RejectionSamples { get; init; } = [];
}
