using System.Diagnostics;
using System.Net;
using Danatadbir.Application.Common.Result;
using Danatadbir.Application.IngestionService;
using Danatadbir.Application.IngestionService.Dtos;
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
            "Ingestion started for {Source} with {SensorCount} sensors and {MetricCount} metrics registered",
            source, knownSensors.Count, knownMetrics.Count);

        var stopwatch = Stopwatch.StartNew();
        var state = new IngestionState(_options.RejectionSampleLimit);
        var batch = new List<SensorData>(_options.WriteBatchSize);
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

            if (!state.Seen.Add(reading.Identity))
            {
                state.Reject(lineNumber, RejectionReason.Duplicate,
                    $"{reading.SensorExternalId}/{reading.MetricKey} at {reading.Timestamp:o} seq {reading.Seq}");
                continue;
            }

            batch.Add(reading);

            if (batch.Count >= _options.WriteBatchSize)
            {
                await sensorDataRepository.WriteAsync(batch, cancellationToken);
                state.StoredReadings += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await sensorDataRepository.WriteAsync(batch, cancellationToken);
            state.StoredReadings += batch.Count;
        }

        stopwatch.Stop();
        var report = state.ToReport(source, stopwatch.Elapsed.TotalMilliseconds);

        logger.LogInformation(
            "Ingestion of {Source} finished in {DurationMs:F0} ms: {Read} lines read, {Stored} stored, "
            + "{Duplicates} duplicates, {Malformed} malformed, {Invalid} invalid, {Unknown} unknown sensor/metric",
            report.Source, report.DurationMs, report.TotalLinesRead, report.StoredReadings,
            report.DuplicatesRemoved, report.MalformedLines, report.InvalidRecords, report.UnknownSensorOrMetric);

        return BaseResult<IngestionReportDto>.Ok(report);
    }

    private sealed class IngestionState(int rejectionSampleLimit)
    {
        private readonly List<RejectedLineDto> _samples = [];

        public HashSet<(string, string, DateTime, long)> Seen { get; } = [];

        public int TotalLinesRead;
        public int ParsedReadings;
        public int StoredReadings;

        private int _duplicates;
        private int _malformed;
        private int _invalid;
        private int _unknown;

        public void Reject(int lineNumber, RejectionReason reason, string detail)
        {
            switch (reason)
            {
                case RejectionReason.Malformed: _malformed++; break;
                case RejectionReason.InvalidField: _invalid++; break;
                case RejectionReason.UnknownSensorOrMetric: _unknown++; break;
                case RejectionReason.Duplicate: _duplicates++; break;
            }

            if (_samples.Count < rejectionSampleLimit)
                _samples.Add(new RejectedLineDto(lineNumber, reason, detail));
        }

        public IngestionReportDto ToReport(string source, double durationMs) => new()
        {
            Source = source,
            TotalLinesRead = TotalLinesRead,
            ParsedReadings = ParsedReadings,
            StoredReadings = StoredReadings,
            DuplicatesRemoved = _duplicates,
            MalformedLines = _malformed,
            InvalidRecords = _invalid,
            UnknownSensorOrMetric = _unknown,
            DurationMs = durationMs,
            RejectionSamples = _samples
        };
    }
}
