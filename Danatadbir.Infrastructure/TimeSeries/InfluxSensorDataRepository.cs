using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Repositories;
using Danatadbir.Infrastructure.Options;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Options;

namespace Danatadbir.Infrastructure.TimeSeries;

public class InfluxSensorDataRepository(IInfluxDBClient client, IOptions<InfluxDbOptions> options)
    : ISensorDataRepository
{
    public const string Measurement = "sensor_data";

    private const string SensorTag = "sensor";
    private const string MetricTag = "metric";
    private const string SeqTag = "seq";
    private const string ValueField = "value";

    private readonly InfluxDbOptions _options = options.Value;

    public Task WriteAsync(IReadOnlyCollection<SensorData> readings, CancellationToken cancellationToken = default)
    {
        if (readings.Count == 0)
            return Task.CompletedTask;

        var points = readings.Select(ToPoint).ToList();

        return client.GetWriteApiAsync()
            .WritePointsAsync(points, _options.Bucket, _options.Organization, cancellationToken);
    }

    public async Task<List<SensorData>> GetRangeAsync(
        string sensorExternalId,
        string metricKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var flux = $"""
                    from(bucket: "{_options.Bucket}")
                      |> range(start: {from:o}, stop: {to:o})
                      |> filter(fn: (r) => r._measurement == "{Measurement}")
                      |> filter(fn: (r) => r.{SensorTag} == "{sensorExternalId}")
                      |> filter(fn: (r) => r.{MetricTag} == "{metricKey}")
                      |> filter(fn: (r) => r._field == "{ValueField}")
                    """;

        var tables = await client.GetQueryApi().QueryAsync(flux, _options.Organization, cancellationToken);

        return tables
            .SelectMany(table => table.Records)
            .Select(record => new SensorData(
                record.GetValueByKey(SensorTag)?.ToString() ?? sensorExternalId,
                record.GetValueByKey(MetricTag)?.ToString() ?? metricKey,
                record.GetTimeInDateTime() ?? default,
                Convert.ToDouble(record.GetValue()),
                long.TryParse(record.GetValueByKey(SeqTag)?.ToString(), out var seq) ? seq : 0))
            .ToList();
    }

    private static PointData ToPoint(SensorData reading) =>
        PointData
            .Measurement(Measurement)
            .Tag(SensorTag, reading.SensorExternalId)
            .Tag(MetricTag, reading.MetricKey)
            .Tag(SeqTag, reading.Seq.ToString())
            .Field(ValueField, reading.Value)
            .Timestamp(reading.Timestamp, WritePrecision.Ns);
}
