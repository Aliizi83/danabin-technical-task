using System.Diagnostics;
using System.Net;
using Danatadbir.Application.AlertService;
using Danatadbir.Application.Common.Result;
using Danatadbir.Application.IngestionService;
using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Application.RuleService;
using Danatadbir.Application.RuleService.Dtos;
using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Danatadbir.Infrastructure.Services.Ingestion;

public class IngestionService(
    IReadingLineSource lineSource,
    ISensorDataRepository sensorDataRepository,
    ISensorRepository sensorRepository,
    IMetricRepository metricRepository,
    IRuleResultRepository ruleResultRepository,
    IRuleCatalog ruleCatalog,
    IRuleEvaluationService ruleEvaluationService,
    IEpisodeDetectionService episodeDetectionService,
    IAlertingService alertingService,
    IOptions<IngestionOptions> options,
    ILogger<IngestionService> logger) : IIngestionService
{
    private readonly IngestionOptions _options = options.Value;

    public async Task<BaseResult<IngestionReportDto>> IngestAsync(
        string? path,
        CancellationToken cancellationToken = default)
    {
        var source = string.IsNullOrWhiteSpace(path) ? _options.DefaultFilePath : path.Trim();

        if (!lineSource.Exists(source))
        {
            logger.LogWarning("Ingestion aborted: feed {Source} was not found", source);
            return BaseResult<IngestionReportDto>.Failure(HttpStatusCode.NotFound, $"Feed '{source}' was not found.");
        }

        // Lookup is case-insensitive but yields the registered spelling, so "pump-01" and "PUMP-01"
        // end up as one series instead of two tags in the time-series store.
        var knownSensors = (await sensorRepository.GetAllAsync(cancellationToken))
            .ToDictionary(sensor => sensor.ExternalId, sensor => sensor.ExternalId, StringComparer.OrdinalIgnoreCase);

        var knownMetrics = (await metricRepository.GetAllAsync(cancellationToken))
            .ToDictionary(metric => metric.Key, metric => metric.Key, StringComparer.OrdinalIgnoreCase);

        logger.LogInformation(
            "Ingestion started for {Source}: {SensorCount} sensors, {MetricCount} metrics, {RuleCount} rules loaded",
            source, knownSensors.Count, knownMetrics.Count, ruleCatalog.Rules.Count);

        var stopwatch = Stopwatch.StartNew();
        var state = new IngestionState(_options.RejectionSampleLimit);
        var lineNumber = 0;

        await foreach (var line in lineSource.ReadLinesAsync(source, cancellationToken))
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            state.TotalLinesRead++;

            var outcome = ReadingLineParser.Parse(line);

            if (!outcome.IsParsed)
            {
                Reject(state, lineNumber, outcome.Reason!.Value, outcome.Detail);
                continue;
            }

            var reading = outcome.Reading!;
            state.ParsedReadings++;

            if (!knownSensors.TryGetValue(reading.SensorExternalId, out var sensorId)
                || !knownMetrics.TryGetValue(reading.MetricKey, out var metricKey))
            {
                Reject(state, lineNumber, RejectionReason.UnknownSensorOrMetric,
                    $"'{reading.SensorExternalId}' / '{reading.MetricKey}' is not registered");
                continue;
            }

            reading = reading with { SensorExternalId = sensorId, MetricKey = metricKey };

            // Last wins: a later occurrence of the same reading replaces the earlier one. The
            // winner is only known once the feed ends, so readings are resolved here and
            // evaluated afterwards.
            if (state.Winners.ContainsKey(reading.Identity))
            {
                Reject(state, lineNumber, RejectionReason.Duplicate,
                    $"{reading.SensorExternalId}/{reading.MetricKey} at {reading.Timestamp:o} seq {reading.Seq}");
            }

            state.Winners[reading.Identity] = reading;
        }

        var readingBatch = new List<SensorData>(_options.WriteBatchSize);
        var violationBatch = new List<RuleResult>();

        // Readings are walked in (sensor, metric, event time, seq) order, so a series can be
        // accumulated as we go and handed to the stateful pass the moment it is complete.
        var series = new List<SensorData>();
        (string Sensor, string Metric)? currentSeries = null;

        foreach (var reading in state.Winners.Values
                     .OrderBy(value => value.SensorExternalId)
                     .ThenBy(value => value.MetricKey)
                     .ThenBy(value => value.Timestamp)
                     .ThenBy(value => value.Seq))
        {
            var key = (reading.SensorExternalId, reading.MetricKey);

            if (currentSeries is not null && currentSeries != key)
            {
                state.RecordEpisodes(DetectEpisodes(currentSeries.Value, series));
                series.Clear();
            }

            currentSeries = key;
            series.Add(reading);

            var evaluation = ruleEvaluationService.Evaluate(reading);
            state.RecordEvaluation(evaluation);

            foreach (var violation in evaluation.Violations)
            {
                violationBatch.Add(new RuleResult
                {
                    SensorExternalId = reading.SensorExternalId,
                    MetricKey = reading.MetricKey,
                    Timestamp = reading.Timestamp,
                    Seq = reading.Seq,
                    Value = reading.Value,
                    RuleId = violation.RuleId,
                    RuleName = violation.RuleName,
                    Reason = violation.Reason
                });
            }

            readingBatch.Add(reading);

            if (readingBatch.Count >= _options.WriteBatchSize)
                state.StoredReadings += await FlushReadingsAsync(readingBatch, cancellationToken);
        }

        if (currentSeries is not null)
            state.RecordEpisodes(DetectEpisodes(currentSeries.Value, series));

        state.StoredReadings += await FlushReadingsAsync(readingBatch, cancellationToken);
        var sync = await ruleResultRepository.SyncAsync(state.Winners.Values.ToList(), violationBatch, cancellationToken);
        state.RuleViolationsStored = sync.Inserted;
        state.RuleViolationsRemoved = sync.Removed;

        // Alerts come last: they derive from episodes, which exist only once every series is scanned.
        state.RecordAlerting(await alertingService.ProcessAsync(state.EpisodeResults, cancellationToken));

        stopwatch.Stop();
        var report = state.ToReport(
            source, stopwatch.Elapsed.TotalMilliseconds, ruleCatalog.Rules.Count, ruleCatalog.RejectedRules.Count);

        foreach (var episode in report.Episodes)
        {
            logger.LogInformation(
                "Sustained episode: rule {RuleId} on {Sensor}/{Metric} from {StartTs:o} to {EndTs:o} "
                + "({DurationSeconds:F0}s, peak {PeakValue}, {ReadingCount} readings)",
                episode.RuleId, episode.SensorExternalId, episode.MetricKey, episode.StartTs,
                episode.EndTs, episode.DurationSeconds, episode.PeakValue, episode.ReadingCount);
        }

        if (state.RejectionsTotal > state.RejectionsLogged)
        {
            logger.LogWarning(
                "{Count} further rejected lines were counted but not logged individually",
                state.RejectionsTotal - state.RejectionsLogged);
        }

        logger.LogInformation("{Report}", FormatReport(report));

        return BaseResult<IngestionReportDto>.Ok(report);
    }

    /// <summary>
    /// Records a rejection and logs it. Bad lines are logged individually as warnings up to the
    /// sample limit, so a badly broken file cannot flood the log; duplicates are a policy outcome
    /// rather than a fault and are only logged at debug level.
    /// </summary>
    private void Reject(IngestionState state, int lineNumber, RejectionReason reason, string detail)
    {
        state.Reject(lineNumber, reason, detail);

        if (reason == RejectionReason.Duplicate)
        {
            logger.LogDebug("Line {LineNumber} is a duplicate: {Detail}", lineNumber, detail);
            return;
        }

        if (state.RejectionsLogged < _options.RejectionSampleLimit)
        {
            state.RejectionsLogged++;
            logger.LogWarning("Line {LineNumber} rejected as {Reason}: {Detail}", lineNumber, reason, detail);
        }
    }

    private static string FormatReport(IngestionReportDto report)
    {
        var rejected = report.MalformedLines + report.InvalidRecords + report.UnknownSensorOrMetric;

        return $"""
                Processing report for {report.Source} ({report.DurationMs:F0} ms)
                  total lines read .............. {report.TotalLinesRead}
                  parsed readings ............... {report.ParsedReadings}
                  stored readings ............... {report.StoredReadings}
                  duplicates removed ............ {report.DuplicatesRemoved}
                  invalid records rejected ...... {rejected} ({report.MalformedLines} malformed, {report.InvalidRecords} invalid, {report.UnknownSensorOrMetric} unknown sensor/metric)
                  rules loaded .................. {report.RulesLoaded} ({report.RulesRejected} rejected)
                  rule evaluations performed .... {report.RuleEvaluationsPerformed}
                  acceptable readings ........... {report.AcceptableReadings}
                  unacceptable readings ......... {report.UnacceptableReadings}
                  rule violations ............... {report.RuleViolations} ({report.RuleViolationsStored} newly stored, {report.RuleViolationsRemoved} stale removed)
                  sustained episodes ............ {report.SustainedEpisodes}
                  alerts generated .............. {report.AlertsGenerated} ({report.AlertsStored} newly stored, {report.EpisodesSuppressed} episodes suppressed by cooldown)
                """;
    }

    private IReadOnlyList<RuleEpisodeDto> DetectEpisodes(
        (string Sensor, string Metric) key,
        List<SensorData> series) =>
        episodeDetectionService.Detect(key.Sensor, key.Metric, series);

    private async Task<int> FlushReadingsAsync(List<SensorData> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return 0;

        await sensorDataRepository.WriteAsync(batch, cancellationToken);
        var written = batch.Count;
        batch.Clear();

        return written;
    }
}
