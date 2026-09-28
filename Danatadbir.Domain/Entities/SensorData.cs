namespace Danatadbir.Domain.Entities;

public record SensorData(
    string SensorExternalId,
    string MetricKey,
    DateTime Timestamp,
    double Value,
    long Seq)
{
    public (string SensorExternalId, string MetricKey, DateTime Timestamp, long Seq) Identity =>
        (SensorExternalId, MetricKey, Timestamp, Seq);
}
