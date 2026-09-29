using Danatadbir.Application.ReadingService.Dtos;
using Danatadbir.Tests.Fakes;
using static Danatadbir.Tests.Fakes.Pipeline;

namespace Danatadbir.Tests.Readings;

public class ReadingQueryServiceTests
{
    // temp-high: temperature > 90 is unacceptable. Minute buckets from t=0.
    private static async Task<Pipeline> Loaded()
    {
        var pipeline = new Pipeline();
        await pipeline.RunAsync(
            Line(0, 60), Line(20, 70), Line(40, 500),          // bucket 0: 500 is unacceptable
            Line(60, 80), Line(80, 100),                       // bucket 1: 100 is unacceptable
            Line(180, 50),                                     // bucket 3 (bucket 2 is empty)
            Line(0, 10, sensor: "PUMP-02"));                   // another sensor
        return pipeline;
    }

    private static AggregationQueryDto Query(int bucketSeconds = 60, double from = 0, double to = 240,
        string? device = "PUMP-01", string? metric = "temperature") =>
        new(device, metric, TestData.At(from), TestData.At(to), bucketSeconds);

    [Fact]
    public async Task Aggregates_are_computed_over_acceptable_readings_only()
    {
        var result = await (await Loaded()).Queries.AggregateAsync(Query());

        Assert.True(result.Success);
        var buckets = result.Data!.Buckets;
        Assert.Equal(new BucketDto(TestData.At(0), 2, 65, 60, 70), buckets[0]);   // 500 excluded
        Assert.Equal(new BucketDto(TestData.At(60), 1, 80, 80, 80), buckets[1]);  // 100 excluded
        Assert.Equal(new BucketDto(TestData.At(180), 1, 50, 50, 50), buckets[2]);
    }

    [Fact]
    public async Task The_result_says_how_many_readings_it_used_and_how_many_it_left_out()
    {
        var data = (await (await Loaded()).Queries.AggregateAsync(Query())).Data!;

        Assert.Equal(4, data.AcceptableReadings);
        Assert.Equal(2, data.ExcludedUnacceptable);
    }

    [Fact]
    public async Task An_unacceptable_reading_never_moves_the_average_min_or_max()
    {
        var data = (await (await Loaded()).Queries.AggregateAsync(Query(bucketSeconds: 240))).Data!;

        var bucket = Assert.Single(data.Buckets);
        Assert.Equal(50, bucket.Min);
        Assert.Equal(80, bucket.Max);              // not 500 or 100
        Assert.Equal(65, bucket.Average, precision: 9);   // (60 + 70 + 80 + 50) / 4
    }

    [Fact]
    public async Task Empty_buckets_are_omitted()
    {
        var data = (await (await Loaded()).Queries.AggregateAsync(Query())).Data!;

        Assert.DoesNotContain(data.Buckets, b => b.Start == TestData.At(120));
    }

    [Fact]
    public async Task Only_the_requested_sensor_and_metric_are_aggregated()
    {
        var data = (await (await Loaded()).Queries.AggregateAsync(Query(device: "PUMP-02"))).Data!;

        Assert.Equal(1, Assert.Single(data.Buckets).Count);
    }

    [Fact]
    public async Task The_range_is_half_open_and_only_covers_from_to_to()
    {
        var data = (await (await Loaded()).Queries.AggregateAsync(Query(from: 60, to: 180))).Data!;

        Assert.Equal(1, data.AcceptableReadings);   // t=60; t=80 is unacceptable; t=180 is outside
    }

    [Fact]
    public async Task Sensor_and_metric_are_matched_case_insensitively_and_echoed_as_registered()
    {
        var data = (await (await Loaded()).Queries.AggregateAsync(Query(device: "pump-01", metric: "TEMPERATURE"))).Data!;

        Assert.Equal("PUMP-01", data.DeviceId);
        Assert.Equal("temperature", data.Metric);
        Assert.NotEmpty(data.Buckets);
    }

    [Fact]
    public async Task A_range_with_no_data_gives_an_empty_result_not_an_error()
    {
        var result = await (await Loaded()).Queries.AggregateAsync(Query(from: 5000, to: 6000));

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Buckets);
    }

    [Theory]
    [InlineData(null, "temperature", 60, 400)]
    [InlineData("PUMP-01", null, 60, 400)]
    [InlineData("PUMP-01", "temperature", 0, 400)]
    [InlineData("PUMP-01", "temperature", -5, 400)]
    [InlineData("GHOST", "temperature", 60, 404)]
    [InlineData("PUMP-01", "humidity", 60, 404)]
    public async Task Bad_input_is_answered_with_a_status_not_an_exception(string? device, string? metric, int bucket, int status)
    {
        var result = await (await Loaded()).Queries.AggregateAsync(Query(bucket, device: device, metric: metric));

        Assert.False(result.Success);
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task An_inverted_range_is_a_bad_request()
    {
        var result = await (await Loaded()).Queries.AggregateAsync(Query(from: 100, to: 100));

        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task A_range_that_would_make_too_many_buckets_is_refused()
    {
        var result = await (await Loaded()).Queries.AggregateAsync(Query(bucketSeconds: 1, from: 0, to: 1_000_000));

        Assert.Equal(400, result.StatusCode);
    }

    // ------------------------------------------------------------ the two lists

    private static ReadingsQueryDto Reading(int page = 1, int pageSize = 100, string? device = "PUMP-01") =>
        new(device, "temperature", TestData.At(0), TestData.At(1000), page, pageSize);

    [Fact]
    public async Task The_acceptable_and_unacceptable_lists_partition_the_stored_readings()
    {
        var pipeline = await Loaded();

        var acceptable = (await pipeline.Queries.GetAcceptableAsync(Reading())).Data!;
        var unacceptable = (await pipeline.Queries.GetUnacceptableAsync(Reading())).Data!;

        Assert.Equal(4, acceptable.Count);
        Assert.Equal(2, unacceptable.Count);
        Assert.Empty(acceptable.Select(r => r.Timestamp).Intersect(unacceptable.Select(r => r.Timestamp)));
        Assert.Equal(6, acceptable.Count + unacceptable.Count);
    }

    [Fact]
    public async Task Unacceptable_readings_name_the_rule_they_broke_and_why()
    {
        var unacceptable = (await (await Loaded()).Queries.GetUnacceptableAsync(Reading())).Data!;

        Assert.Equal([500d, 100d], unacceptable.Select(r => r.Value));
        var violation = Assert.Single(unacceptable[0].Violations);
        Assert.Equal("temp-high", violation.RuleId);
        Assert.Contains("500", violation.Reason);
    }

    [Fact]
    public async Task Readings_are_listed_in_time_order()
    {
        var acceptable = (await (await Loaded()).Queries.GetAcceptableAsync(Reading())).Data!;

        Assert.Equal(acceptable.OrderBy(r => r.Timestamp).Select(r => r.Timestamp), acceptable.Select(r => r.Timestamp));
    }

    [Fact]
    public async Task Lists_are_paged_and_report_the_total()
    {
        var result = await (await Loaded()).Queries.GetAcceptableAsync(Reading(page: 2, pageSize: 3));

        Assert.Single(result.Data!);
        Assert.Equal(4, result.PaginationMetaData!.TotalCount);
        Assert.Equal(2, result.PaginationMetaData.PageNumber);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 100000)]
    public async Task Bad_paging_is_a_bad_request(int page, int pageSize)
    {
        var pipeline = await Loaded();

        Assert.Equal(400, (await pipeline.Queries.GetAcceptableAsync(Reading(page, pageSize))).StatusCode);
        Assert.Equal(400, (await pipeline.Queries.GetUnacceptableAsync(Reading(page, pageSize))).StatusCode);
    }

    [Fact]
    public async Task A_reading_corrected_by_a_later_run_moves_from_the_unacceptable_list_to_the_acceptable_one()
    {
        var pipeline = new Pipeline();
        await pipeline.RunAsync(Line(0, 500));
        Assert.Single((await pipeline.Queries.GetUnacceptableAsync(Reading())).Data!);

        await pipeline.RunAsync(Line(0, 50));

        Assert.Empty((await pipeline.Queries.GetUnacceptableAsync(Reading())).Data!);
        Assert.Single((await pipeline.Queries.GetAcceptableAsync(Reading())).Data!);
    }
}
