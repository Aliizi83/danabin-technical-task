using System.Diagnostics;
using System.Net;
using Danatadbir.Application.Common.Result;
using Danatadbir.Application.IngestionService;
using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Application.RuleService;
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

        var knownSensors = (await sensorRepository.GetAllAsync(cancellationToken))
            .Select(sensor => sensor.ExternalId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var knownMetrics = (await metricRepository.GetAllAsync(cancellationToken))
            .Select(metric => metric.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                state.Reject(lineNumber, outcome.Reason!.Value, outcome.Detail);
                continue;
            }

            var reading = outcome.Reading!;
            state.ParsedReadings++;

            if (!knownSensors.Contains(reading.SensorExternalId) || !knownMetrics.Contains(reading.MetricKey))
            {
                state.Reject(lineNumber, RejectionReason.UnknownSensorOrMetric,
                    $"'{reading.SensorExternalId}' / '{reading.MetricKey}' is not registered");
                continue;
            }

            // Last wins: a later occurrence of the same reading replaces the earlier one. The
            // winner is only known once the feed ends, so readings are resolved here and
            // evaluated afterwards.
            if (state.Winners.ContainsKey(reading.Identity))
            {
                state.Reject(lineNumber, RejectionReason.Duplicate,
                    $"{reading.SensorExternalId}/{reading.MetricKey} at {reading.Timestamp:o} seq {reading.Seq}");
            }

            state.Winners[reading.Identity] = reading;
        }

        var readingBatch = new List<SensorData>(_options.WriteBatchSize);
        var violationBatch = new List<RuleResult>();

        foreach (var reading in state.Winners.Values
                     .OrderBy(value => value.SensorExternalId)
                     .ThenBy(value => value.MetricKey)
                     .ThenBy(value => value.Timestamp)
                     .ThenBy(value => value.Seq))
        {
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

        state.StoredReadings += await FlushReadingsAsync(readingBatch, cancellationToken);
        state.RuleViolationsStored = await ruleResultRepository.AddMissingAsync(violationBatch, cancellationToken);

        stopwatch.Stop();
        var report = state.ToReport(
            source, stopwatch.Elapsed.TotalMilliseconds, ruleCatalog.Rules.Count, ruleCatalog.RejectedRules.Count);

        logger.LogInformation(
            "Ingestion of {Source} finished in {DurationMs:F0} ms: {Read} lines read, {Stored} stored, "
            + "{Duplicates} duplicates, {Malformed} malformed, {Invalid} invalid, {Unknown} unknown sensor/metric",
            report.Source, report.DurationMs, report.TotalLinesRead, report.StoredReadings,
            report.DuplicatesRemoved, report.MalformedLines, report.InvalidRecords, report.UnknownSensorOrMetric);

        logger.LogInformation(
            "Rule evaluation: {Evaluations} evaluations over {Rules} rules, {Acceptable} acceptable, "
            + "{Unacceptable} unacceptable, {Violations} violations ({StoredViolations} newly stored)",
            report.RuleEvaluationsPerformed, report.RulesLoaded, report.AcceptableReadings,
            report.UnacceptableReadings, report.RuleViolations, report.RuleViolationsStored);

        return BaseResult<IngestionReportDto>.Ok(report);
    }

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
