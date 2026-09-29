using Danatadbir.Tests.Fakes;
using static Danatadbir.Tests.Fakes.Pipeline;

namespace Danatadbir.Tests.Ingestion;

/// <summary>
/// Processing the same file twice must not create a second copy of a reading, a rule result or an
/// alert. The in-memory stores enforce the same natural keys as the real ones:
///   reading      (sensor, metric, ts, seq)            — an InfluxDB point identity
///   rule result  (sensor, metric, ts, seq, rule)      — unique index
///   alert        (rule, sensor, metric, startTs)      — unique index
/// </summary>
public class IdempotencyTests
{
    private static List<string> Feed() =>
    [
        Line(0, 95), Line(10, 60), Line(20, 500),                                   // two violations
        Line(30, -2, metric: "vibration"),                                          // one violation
        Line(0, 95),                                                                // a duplicate
        "{bad json", Line(1, 1, sensor: "GHOST"),                                   // rejected
        ..Enumerable.Range(0, 8).Select(i => Line(100 + i * 10, 75)),               // episode 1, starts at 100
        ..Enumerable.Range(0, 8).Select(i => Line(220 + i * 10, 75)),               // episode 2, inside the cooldown
        ..Enumerable.Range(0, 8).Select(i => Line(900 + i * 10, 75))                // episode 3, well after
    ];

    private static object Snapshot(Pipeline pipeline) => new
    {
        Points = string.Join("|", pipeline.Readings.Points.Values.OrderBy(r => r.Identity).Select(r => $"{r.Identity}={r.Value}")),
        Rows = string.Join("|", pipeline.RuleResults.Rows.OrderBy(r => r.RuleId).ThenBy(r => r.Timestamp).Select(r => $"{r.RuleId}@{r.Timestamp:o}#{r.Seq}")),
        Alerts = string.Join("|", pipeline.Alerts.Rows.OrderBy(a => a.StartTs).Select(a => $"{a.RuleId}@{a.StartTs:o}")),
    };

    [Fact]
    public async Task Running_the_same_file_twice_creates_no_duplicate_readings_results_or_alerts()
    {
        var pipeline = new Pipeline();

        var first = await pipeline.RunAsync(Feed());
        var afterFirst = Snapshot(pipeline);
        var second = await pipeline.RunAsync(Feed());

        Assert.Equal(afterFirst, Snapshot(pipeline));
        Assert.Equal(first.StoredReadings, pipeline.Readings.Points.Count);
        Assert.Equal(first.RuleViolations, pipeline.RuleResults.Rows.Count);
        Assert.Equal(first.AlertsGenerated, pipeline.Alerts.Rows.Count);
    }

    [Fact]
    public async Task The_second_run_reports_the_same_input_counts_and_zero_new_writes()
    {
        var pipeline = new Pipeline();

        var first = await pipeline.RunAsync(Feed());
        var second = await pipeline.RunAsync(Feed());

        // Counts that describe the input are identical...
        Assert.Equal(first.TotalLinesRead, second.TotalLinesRead);
        Assert.Equal(first.StoredReadings, second.StoredReadings);
        Assert.Equal(first.DuplicatesRemoved, second.DuplicatesRemoved);
        Assert.Equal(first.RuleViolations, second.RuleViolations);
        Assert.Equal(first.SustainedEpisodes, second.SustainedEpisodes);
        Assert.Equal(first.AlertsGenerated, second.AlertsGenerated);
        Assert.Equal(first.EpisodesSuppressed, second.EpisodesSuppressed);

        // ...while what was newly written drops to nothing.
        Assert.True(first.RuleViolationsStored > 0);
        Assert.Equal(0, second.RuleViolationsStored);
        Assert.Equal(0, second.RuleViolationsRemoved);
        Assert.True(first.AlertsStored > 0);
        Assert.Equal(0, second.AlertsStored);
    }

    [Fact]
    public async Task Ten_runs_of_the_same_file_leave_the_stores_exactly_as_one_run_did()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Feed());
        var afterFirst = Snapshot(pipeline);

        for (var i = 0; i < 9; i++)
            await pipeline.RunAsync(Feed());

        Assert.Equal(afterFirst, Snapshot(pipeline));
    }

    [Fact]
    public async Task A_shuffled_copy_of_the_file_is_the_same_data_and_changes_nothing()
    {
        var pipeline = new Pipeline();
        await pipeline.RunAsync(Feed());
        var afterFirst = Snapshot(pipeline);

        var second = await pipeline.RunAsync(Feed().OrderBy(l => l.GetHashCode()).ToList());

        Assert.Equal(afterFirst, Snapshot(pipeline));
        Assert.Equal(0, second.RuleViolationsStored);
        Assert.Equal(0, second.AlertsStored);
    }

    [Fact]
    public async Task Two_files_with_overlapping_readings_store_the_overlap_once()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 95), Line(10, 60), Line(20, 60));
        var second = await pipeline.RunAsync(Line(10, 60), Line(20, 60), Line(30, 95));

        Assert.Equal(4, pipeline.Readings.Points.Count);
        Assert.Equal(2, pipeline.RuleResults.Rows.Count);
        Assert.Equal(1, second.RuleViolationsStored);
    }

    // ------------------------------------------------------------ a reading whose value changes

    [Fact]
    public async Task A_corrected_reading_loses_the_violation_it_no_longer_has()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 500));
        Assert.Single(pipeline.RuleResults.Rows);

        var corrected = await pipeline.RunAsync(Line(0, 50));

        Assert.Empty(pipeline.RuleResults.Rows);
        Assert.Equal(1, corrected.RuleViolationsRemoved);
        Assert.Equal(50, Assert.Single(pipeline.Readings.Points.Values).Value);
    }

    [Fact]
    public async Task A_reading_that_starts_violating_gains_the_violation()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 50));
        await pipeline.RunAsync(Line(0, 500));

        Assert.Single(pipeline.RuleResults.Rows);
    }

    [Fact]
    public async Task A_violation_whose_value_changes_is_updated_not_duplicated()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 500));
        await pipeline.RunAsync(Line(0, 600));

        var row = Assert.Single(pipeline.RuleResults.Rows);
        Assert.Equal(600, row.Value);
    }

    [Fact]
    public async Task Correcting_one_reading_leaves_the_violations_of_other_readings_alone()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 500), Line(10, 500));
        await pipeline.RunAsync(Line(0, 50));

        var row = Assert.Single(pipeline.RuleResults.Rows);
        Assert.Equal(TestData.At(10), row.Timestamp);
    }

    [Fact]
    public async Task Violations_of_readings_not_in_the_new_file_are_untouched()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 500));
        await pipeline.RunAsync(Line(100, 50));

        Assert.Single(pipeline.RuleResults.Rows);
    }
}
