using Danatadbir.Application.AlertService.Dtos;
using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Application.RuleService.Dtos;
using Danatadbir.Domain.Entities;

namespace Danatadbir.Infrastructure.Services.Ingestion;

internal sealed class IngestionState(int sampleLimit)
{
    private readonly List<RejectedLineDto> _rejectionSamples = [];
    private readonly List<ViolationSampleDto> _violationSamples = [];

    private int _duplicates;
    private int _malformed;
    private int _invalid;
    private int _unknown;

    public Dictionary<(string, string, DateTime, long), SensorData> Winners { get; } = [];

    public int TotalLinesRead;
    public int ParsedReadings;
    public int StoredReadings;
    public int RuleViolationsStored;
    public int RuleViolationsRemoved;

    private int _evaluations;
    private int _acceptable;
    private int _unacceptable;
    private int _violations;

    private readonly List<EpisodeSampleDto> _episodes = [];
    private AlertingResultDto _alerting = AlertingResultDto.Empty;

    public List<RuleEpisodeDto> EpisodeResults { get; } = [];

    public void Reject(int lineNumber, RejectionReason reason, string detail)
    {
        switch (reason)
        {
            case RejectionReason.Malformed: _malformed++; break;
            case RejectionReason.InvalidField: _invalid++; break;
            case RejectionReason.UnknownSensorOrMetric: _unknown++; break;
            case RejectionReason.Duplicate: _duplicates++; break;
        }

        if (_rejectionSamples.Count < sampleLimit)
            _rejectionSamples.Add(new RejectedLineDto(lineNumber, reason, detail));
    }

    public void RecordEvaluation(ReadingEvaluationDto evaluation)
    {
        _evaluations += evaluation.EvaluationsPerformed;
        _violations += evaluation.Violations.Count;

        if (evaluation.IsAcceptable)
        {
            _acceptable++;
            return;
        }

        _unacceptable++;

        foreach (var violation in evaluation.Violations)
        {
            if (_violationSamples.Count >= sampleLimit)
                return;

            _violationSamples.Add(new ViolationSampleDto(
                evaluation.Reading.SensorExternalId,
                evaluation.Reading.MetricKey,
                evaluation.Reading.Timestamp,
                evaluation.Reading.Value,
                violation.RuleId,
                violation.Reason));
        }
    }

    public void RecordAlerting(AlertingResultDto alerting) => _alerting = alerting;

    public void RecordEpisodes(IReadOnlyList<RuleEpisodeDto> episodes)
    {
        EpisodeResults.AddRange(episodes);

        foreach (var episode in episodes)
        {
            _episodes.Add(new EpisodeSampleDto(
                episode.RuleId,
                episode.SensorExternalId,
                episode.MetricKey,
                episode.Episode.StartTs,
                episode.Episode.EndTs,
                episode.Episode.Duration.TotalSeconds,
                episode.Episode.PeakValue,
                episode.Episode.ReadingCount));
        }
    }

    public IngestionReportDto ToReport(string source, double durationMs, int rulesLoaded, int rulesRejected) => new()
    {
        Source = source,
        TotalLinesRead = TotalLinesRead,
        ParsedReadings = ParsedReadings,
        StoredReadings = StoredReadings,
        DuplicatesRemoved = _duplicates,
        MalformedLines = _malformed,
        InvalidRecords = _invalid,
        UnknownSensorOrMetric = _unknown,
        RulesLoaded = rulesLoaded,
        RulesRejected = rulesRejected,
        RuleEvaluationsPerformed = _evaluations,
        AcceptableReadings = _acceptable,
        UnacceptableReadings = _unacceptable,
        RuleViolations = _violations,
        RuleViolationsStored = RuleViolationsStored,
        RuleViolationsRemoved = RuleViolationsRemoved,
        SustainedEpisodes = _episodes.Count,
        AlertsGenerated = _alerting.AlertsGenerated,
        AlertsStored = _alerting.AlertsStored,
        EpisodesSuppressed = _alerting.EpisodesSuppressed,
        DurationMs = durationMs,
        RejectionSamples = _rejectionSamples,
        ViolationSamples = _violationSamples,
        Episodes = _episodes,
        Alerts = _alerting.Alerts,
        SuppressedEpisodes = _alerting.Suppressed
    };
}
