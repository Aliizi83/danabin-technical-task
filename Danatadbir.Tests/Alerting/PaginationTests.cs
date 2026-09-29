using Danatadbir.Application.ReadingService.Dtos;
using Danatadbir.Tests.Fakes;
using static Danatadbir.Tests.Fakes.Pipeline;

namespace Danatadbir.Tests.Alerting;

public class PaginationTests
{
    private static IEnumerable<string> Episode(string sensor) =>
        Enumerable.Range(0, 8).Select(i => Line(i * 10, 75, sensor: sensor));

    /// <summary>Four sensors with identical episodes: four alerts of one rule with the same start time.</summary>
    private static async Task<Pipeline> WithTiedAlerts()
    {
        var pipeline = new Pipeline(TestCatalog.Rule("fleet", "temperature", "SustainedAbove", Sustained));
        await pipeline.RunAsync(new[] { "PUMP-01", "PUMP-02", "COMP-01", "FAN-03" }.SelectMany(Episode));
        return pipeline;
    }

    [Fact]
    public async Task Alerts_that_share_a_rule_and_a_start_time_are_still_in_a_stable_order_across_pages()
    {
        var pipeline = await WithTiedAlerts();
        var all = (await pipeline.AlertQueries.GetAsync(null, null, null, null, null, 1, 100)).Data!;
        Assert.Single(all.Select(a => a.StartTs).Distinct());   // the alerts really do tie on start time

        var walked = new List<string>();
        for (var page = 1; page <= 4; page++)
        {
            var one = (await pipeline.AlertQueries.GetAsync(null, null, null, null, null, page, 1)).Data!;
            walked.AddRange(one.Select(a => a.SensorExternalId));
        }

        Assert.Equal(all.Select(a => a.SensorExternalId), walked);
        Assert.Equal(4, walked.Distinct().Count());
    }

    [Fact]
    public async Task A_huge_page_number_is_an_empty_page_not_the_first_one()
    {
        var pipeline = await WithTiedAlerts();

        var alerts = await pipeline.AlertQueries.GetAsync(null, null, null, null, null, int.MaxValue, 1000);
        var acceptable = await pipeline.Queries.GetAcceptableAsync(
            new ReadingsQueryDto("PUMP-01", "temperature", TestData.At(0), TestData.At(1000), int.MaxValue, 1000));
        var unacceptable = await pipeline.Queries.GetUnacceptableAsync(
            new ReadingsQueryDto("PUMP-01", "temperature", TestData.At(0), TestData.At(1000), int.MaxValue, 1000));

        Assert.Empty(alerts.Data!);
        Assert.Empty(acceptable.Data!);
        Assert.Empty(unacceptable.Data!);
        Assert.Equal(4, alerts.PaginationMetaData!.TotalCount);      // the total is still reported
        Assert.Equal(8, acceptable.PaginationMetaData!.TotalCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(25)]
    [InlineData(40)]
    public async Task Walking_every_page_yields_every_reading_once_in_order_and_the_metadata_is_right(int pageSize)
    {
        var pipeline = new Pipeline();
        await pipeline.RunAsync(Enumerable.Range(0, 25).Select(i => Line(i * 10, i % 4 == 0 ? 500 : 60 + i)));   // 7 unacceptable, 18 acceptable
        ReadingsQueryDto Query(int page) => new("PUMP-01", "temperature", TestData.At(0), TestData.At(1000), page, pageSize);

        foreach (var (expectedTotal, fetch) in new (int, Func<int, Task<(List<DateTime> Times, int Total, int Pages)>>)[]
                 {
                     (18, async p => { var r = await pipeline.Queries.GetAcceptableAsync(Query(p)); return (r.Data!.Select(x => x.Timestamp).ToList(), r.PaginationMetaData!.TotalCount, r.PaginationMetaData.TotalPages); }),
                     (7, async p => { var r = await pipeline.Queries.GetUnacceptableAsync(Query(p)); return (r.Data!.Select(x => x.Timestamp).ToList(), r.PaginationMetaData!.TotalCount, r.PaginationMetaData.TotalPages); }),
                 })
        {
            var collected = new List<DateTime>();
            for (var page = 1; ; page++)
            {
                var (times, total, pages) = await fetch(page);
                Assert.Equal(expectedTotal, total);
                Assert.Equal((int)Math.Ceiling(expectedTotal / (double)pageSize), pages);
                if (times.Count == 0) break;
                Assert.True(times.Count <= pageSize);
                collected.AddRange(times);
            }

            Assert.Equal(expectedTotal, collected.Count);
            Assert.Equal(expectedTotal, collected.Distinct().Count());
            Assert.Equal(collected.Order(), collected);
        }
    }

    [Fact]
    public async Task The_last_page_holds_the_remainder()
    {
        var pipeline = new Pipeline();
        await pipeline.RunAsync(Enumerable.Range(0, 10).Select(i => Line(i * 10, 60)));

        var last = await pipeline.Queries.GetAcceptableAsync(
            new ReadingsQueryDto("PUMP-01", "temperature", TestData.At(0), TestData.At(1000), 4, 3));

        Assert.Single(last.Data!);
        Assert.Equal(4, last.PaginationMetaData!.TotalPages);
    }

    [Fact]
    public async Task An_empty_result_reports_zero_total_and_zero_pages()
    {
        var pipeline = new Pipeline();

        var result = await pipeline.Queries.GetAcceptableAsync(
            new ReadingsQueryDto("PUMP-01", "temperature", TestData.At(0), TestData.At(1000)));

        Assert.Empty(result.Data!);
        Assert.Equal(0, result.PaginationMetaData!.TotalCount);
        Assert.Equal(0, result.PaginationMetaData.TotalPages);
    }
}
