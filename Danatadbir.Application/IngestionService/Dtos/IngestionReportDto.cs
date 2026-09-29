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

    public int RulesLoaded { get; init; }
    public int RulesRejected { get; init; }
    public int RuleEvaluationsPerformed { get; init; }
    public int AcceptableReadings { get; init; }
    public int UnacceptableReadings { get; init; }
    public int RuleViolations { get; init; }
    public int RuleViolationsStored { get; init; }

    public double DurationMs { get; init; }

    public IReadOnlyList<RejectedLineDto> RejectionSamples { get; init; } = [];

    public IReadOnlyList<ViolationSampleDto> ViolationSamples { get; init; } = [];
}
