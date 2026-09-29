using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Aggregation;

public record AggregateBucket(DateTime Start, int Count, double Average, double Min, double Max);

/// <summary>
/// Groups readings into equally sized buckets that start at <c>from</c>: bucket <c>i</c> covers
/// <c>[from + i*size, from + (i+1)*size)</c>. The range is half-open, so a reading exactly at
/// <c>to</c> is outside it and one exactly on a bucket boundary belongs to the later bucket.
/// The last bucket is cut off at <c>to</c> when the range is not a whole number of buckets.
/// Buckets with no readings are omitted rather than reported with a count of zero.
/// </summary>
public static class TimeBucketAggregator
{
    public static IReadOnlyList<AggregateBucket> Aggregate(
        IEnumerable<SensorData> readings,
        DateTime from,
        DateTime to,
        TimeSpan bucketSize)
    {
        if (bucketSize <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(bucketSize), "Bucket size must be positive.");

        return readings
            .Where(reading => reading.Timestamp >= from && reading.Timestamp < to)
            .GroupBy(reading => (reading.Timestamp - from).Ticks / bucketSize.Ticks)
            .OrderBy(group => group.Key)
            .Select(group => new AggregateBucket(
                from + TimeSpan.FromTicks(group.Key * bucketSize.Ticks),
                group.Count(),
                group.Average(reading => reading.Value),
                group.Min(reading => reading.Value),
                group.Max(reading => reading.Value)))
            .ToList();
    }
}
