using Danatadbir.Domain.Aggregation;
using Danatadbir.Tests.Fakes;

namespace Danatadbir.Tests.Domain;

public class TimeBucketAggregatorTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    [Fact]
    public void Computes_count_average_min_and_max_per_bucket()
    {
        var readings = new[] { TestData.Reading(0, 10), TestData.Reading(20, 20), TestData.Reading(40, 30), TestData.Reading(70, 5) };

        var buckets = TimeBucketAggregator.Aggregate(readings, TestData.At(0), TestData.At(120), Minute);

        Assert.Equal(2, buckets.Count);
        Assert.Equal(new AggregateBucket(TestData.At(0), 3, 20, 10, 30), buckets[0]);
        Assert.Equal(new AggregateBucket(TestData.At(60), 1, 5, 5, 5), buckets[1]);
    }

    [Fact]
    public void Buckets_start_at_from_not_at_a_calendar_boundary()
    {
        var buckets = TimeBucketAggregator.Aggregate([TestData.Reading(50, 1)], TestData.At(45), TestData.At(200), Minute);

        Assert.Equal(TestData.At(45), Assert.Single(buckets).Start);
    }

    [Fact]
    public void A_reading_on_a_bucket_boundary_belongs_to_the_later_bucket()
    {
        var buckets = TimeBucketAggregator.Aggregate([TestData.Reading(60, 1)], TestData.At(0), TestData.At(120), Minute);

        Assert.Equal(TestData.At(60), Assert.Single(buckets).Start);
    }

    [Fact]
    public void The_range_is_half_open()
    {
        var readings = new[] { TestData.Reading(0, 1), TestData.Reading(119, 2), TestData.Reading(120, 3) };

        var buckets = TimeBucketAggregator.Aggregate(readings, TestData.At(0), TestData.At(120), Minute);

        Assert.Equal(2, buckets.Sum(b => b.Count));
    }

    [Fact]
    public void Readings_outside_the_range_are_ignored()
    {
        var readings = new[] { TestData.Reading(-10, 99), TestData.Reading(30, 1), TestData.Reading(500, 99) };

        var buckets = TimeBucketAggregator.Aggregate(readings, TestData.At(0), TestData.At(120), Minute);

        Assert.Equal(1, Assert.Single(buckets).Count);
    }

    [Fact]
    public void Empty_buckets_are_omitted()
    {
        var readings = new[] { TestData.Reading(10, 1), TestData.Reading(310, 2) };

        var buckets = TimeBucketAggregator.Aggregate(readings, TestData.At(0), TestData.At(400), Minute);

        Assert.Equal([TestData.At(0), TestData.At(300)], buckets.Select(b => b.Start));
    }

    [Fact]
    public void A_partial_last_bucket_only_holds_readings_before_to()
    {
        var readings = new[] { TestData.Reading(65, 1), TestData.Reading(85, 2) };

        var buckets = TimeBucketAggregator.Aggregate(readings, TestData.At(0), TestData.At(80), Minute);

        var bucket = Assert.Single(buckets);
        Assert.Equal(1, bucket.Count);
        Assert.Equal(1, bucket.Average);
    }

    [Fact]
    public void An_empty_input_gives_no_buckets() =>
        Assert.Empty(TimeBucketAggregator.Aggregate([], TestData.At(0), TestData.At(60), Minute));

    [Fact]
    public void A_non_positive_bucket_size_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TimeBucketAggregator.Aggregate([], TestData.At(0), TestData.At(60), TimeSpan.Zero));

    [Fact]
    public void Readings_may_arrive_in_any_order()
    {
        var readings = new[] { TestData.Reading(70, 5), TestData.Reading(0, 10), TestData.Reading(40, 30), TestData.Reading(20, 20) };

        var buckets = TimeBucketAggregator.Aggregate(readings, TestData.At(0), TestData.At(120), Minute);

        Assert.Equal(20, buckets[0].Average);
        Assert.Equal(TestData.At(60), buckets[1].Start);
    }
}
