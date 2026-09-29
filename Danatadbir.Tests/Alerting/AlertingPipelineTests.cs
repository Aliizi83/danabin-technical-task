using Danatadbir.Tests.Fakes;
using static Danatadbir.Tests.Fakes.Pipeline;

namespace Danatadbir.Tests.Alerting;

public class AlertingPipelineTests
{
    private static IEnumerable<string> Episode(double from, double seconds = 70, string sensor = "PUMP-01", double value = 75) =>
        Enumerable.Range(0, (int)(seconds / 10) + 1).Select(i => Line(from + i * 10, value, sensor: sensor));

    private static IEnumerable<string> Dip(double at, string sensor = "PUMP-01") => [Line(at, 60, sensor: sensor)];

    [Fact]
    public async Task A_sustained_episode_raises_one_alert_not_one_per_reading()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(Episode(0));

        Assert.Equal(1, report.AlertsGenerated);
        var alert = Assert.Single(pipeline.Alerts.Rows);
        Assert.Equal(8, alert.ReadingCount);
    }

    [Fact]
    public async Task The_alert_carries_rule_sensor_metric_start_end_and_peak()
    {
        var pipeline = new Pipeline();
        var lines = Episode(0).ToList();
        lines[3] = Line(30, 79.5);

        await pipeline.RunAsync(lines);

        var alert = Assert.Single(pipeline.Alerts.Rows);
        Assert.Equal("pump01-sustained", alert.RuleId);
        Assert.Equal("PUMP-01", alert.SensorExternalId);
        Assert.Equal("temperature", alert.MetricKey);
        Assert.Equal(TestData.At(0), alert.StartTs);
        Assert.Equal(TestData.At(70), alert.EndTs);
        Assert.Equal(79.5, alert.PeakValue);
    }

    [Fact]
    public async Task An_episode_still_open_at_the_end_of_the_feed_ends_at_the_last_reading()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Episode(0, seconds: 90));

        Assert.Equal(TestData.At(90), Assert.Single(pipeline.Alerts.Rows).EndTs);
    }

    [Fact]
    public async Task An_episode_that_is_too_short_raises_nothing()
    {
        var report = await new Pipeline().RunAsync(Episode(0, seconds: 50));

        Assert.Equal(0, report.SustainedEpisodes);
        Assert.Equal(0, report.AlertsGenerated);
    }

    [Fact]
    public async Task A_second_episode_inside_the_cooldown_is_detected_but_raises_no_alert()
    {
        var pipeline = new Pipeline();

        // Episode 1 starts at 0; episode 2 starts at 220 s, less than five minutes later.
        var report = await pipeline.RunAsync(Episode(0).Concat(Dip(80)).Concat(Episode(220)));

        Assert.Equal(2, report.SustainedEpisodes);
        Assert.Equal(1, report.AlertsGenerated);
        Assert.Equal(1, report.EpisodesSuppressed);
        var suppressed = Assert.Single(report.SuppressedEpisodes);
        Assert.Equal(TestData.At(0), suppressed.SuppressedByStart);
        Assert.Single(pipeline.Alerts.Rows);
    }

    [Fact]
    public async Task Episodes_further_apart_than_the_cooldown_raise_separate_alerts()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(Episode(0).Concat(Dip(80)).Concat(Episode(400)));

        Assert.Equal(2, report.AlertsGenerated);
        Assert.Equal(0, report.EpisodesSuppressed);
        Assert.Equal(2, pipeline.Alerts.Rows.Count);
    }

    [Fact]
    public async Task An_episode_exactly_one_cooldown_after_the_previous_start_raises_again()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(Episode(0).Concat(Dip(80)).Concat(Episode(300)));

        Assert.Equal(2, report.AlertsGenerated);
    }

    [Fact]
    public async Task The_cooldown_can_be_set_per_rule()
    {
        var pipeline = new Pipeline(TestCatalog.Rule("short", "temperature", "SustainedAbove", Sustained, cooldownSeconds: 60));

        var report = await pipeline.RunAsync(Episode(0).Concat(Dip(80)).Concat(Episode(220)));

        Assert.Equal(2, report.AlertsGenerated);   // 220 s apart, but this rule's cooldown is 60 s
    }

    [Fact]
    public async Task A_fleet_wide_rule_raises_an_alert_per_sensor_and_one_does_not_silence_another()
    {
        var pipeline = new Pipeline(TestCatalog.Rule("fleet", "temperature", "SustainedAbove", Sustained));

        var report = await pipeline.RunAsync(
            Episode(0, sensor: "PUMP-01").Concat(Episode(30, sensor: "PUMP-02")).Concat(Episode(60, sensor: "COMP-01")));

        Assert.Equal(3, report.AlertsGenerated);
        Assert.Equal(0, report.EpisodesSuppressed);
        Assert.Equal(3, pipeline.Alerts.Rows.Select(a => a.SensorExternalId).Distinct().Count());
    }

    [Fact]
    public async Task A_device_specific_rule_ignores_other_sensors()
    {
        var report = await new Pipeline().RunAsync(Episode(0, sensor: "PUMP-02"));   // default rule is for PUMP-01

        Assert.Equal(0, report.SustainedEpisodes);
    }

    [Fact]
    public async Task A_disabled_sustained_rule_raises_nothing()
    {
        var pipeline = new Pipeline(TestCatalog.Rule("off", "temperature", "SustainedAbove", Sustained, enabled: false));

        var report = await pipeline.RunAsync(Episode(0));

        Assert.Equal(0, report.SustainedEpisodes);
        Assert.Empty(pipeline.Alerts.Rows);
    }

    [Fact]
    public async Task Alerts_are_the_same_whatever_order_the_file_arrives_in()
    {
        var feed = Episode(0).Concat(Dip(80)).Concat(Episode(220)).Concat(Dip(300)).Concat(Episode(900)).ToList();
        var forward = new Pipeline();
        var backward = new Pipeline();

        await forward.RunAsync(feed);
        await backward.RunAsync(Enumerable.Reverse(feed));

        Assert.Equal(
            forward.Alerts.Rows.OrderBy(a => a.StartTs).Select(a => (a.StartTs, a.EndTs)),
            backward.Alerts.Rows.OrderBy(a => a.StartTs).Select(a => (a.StartTs, a.EndTs)));
        Assert.Equal(2, forward.Alerts.Rows.Count);
    }

    [Fact]
    public async Task Rerunning_the_same_file_stores_no_second_copy_of_any_alert()
    {
        var pipeline = new Pipeline();
        var feed = Episode(0).Concat(Dip(80)).Concat(Episode(400)).ToList();

        var first = await pipeline.RunAsync(feed);
        var second = await pipeline.RunAsync(feed);

        Assert.Equal(2, first.AlertsStored);
        Assert.Equal(0, second.AlertsStored);
        Assert.Equal(2, second.AlertsGenerated);      // the same two alerts, recognised, not raised again
        Assert.Equal(2, pipeline.Alerts.Rows.Count);
    }

    [Fact]
    public async Task Rerunning_a_file_with_a_suppressed_episode_suppresses_it_again()
    {
        var pipeline = new Pipeline();
        var feed = Episode(0).Concat(Dip(80)).Concat(Episode(220)).ToList();

        await pipeline.RunAsync(feed);
        var second = await pipeline.RunAsync(feed);

        Assert.Equal(1, second.AlertsGenerated);
        Assert.Equal(1, second.EpisodesSuppressed);
        Assert.Equal(0, second.AlertsStored);
        Assert.Single(pipeline.Alerts.Rows);
    }

    [Fact]
    public async Task A_later_file_is_checked_against_the_alerts_stored_by_an_earlier_one()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Episode(0));
        var second = await pipeline.RunAsync(Episode(200));   // 200 s after the stored alert, inside its cooldown

        Assert.Equal(0, second.AlertsGenerated);
        Assert.Equal(1, second.EpisodesSuppressed);
        Assert.Single(pipeline.Alerts.Rows);
    }

    [Fact]
    public async Task A_late_arriving_older_episode_is_not_measured_against_the_newest_alert()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Episode(7200));                // a stored alert two hours in
        var late = await pipeline.RunAsync(Episode(0));        // an older episode, two hours before it

        Assert.Equal(1, late.AlertsGenerated);
        Assert.Equal(2, pipeline.Alerts.Rows.Count);
    }

    [Fact]
    public async Task A_late_episode_just_before_a_stored_alert_is_suppressed_so_no_two_alerts_are_too_close()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Episode(180));                 // stored alert at 180 s
        var late = await pipeline.RunAsync(Episode(0));        // episode at 0 s: 180 s before it

        Assert.Equal(1, late.EpisodesSuppressed);
        Assert.Single(pipeline.Alerts.Rows);
    }

    [Fact]
    public async Task No_two_stored_alerts_of_a_key_are_ever_nearer_than_the_cooldown()
    {
        var pipeline = new Pipeline();
        var random = new Random(5);
        var starts = Enumerable.Range(0, 60).Select(_ => random.Next(0, 6000) / 10 * 10).Distinct().ToList();

        foreach (var start in starts)
            await pipeline.RunAsync(Episode(start).Concat(Dip(start + 80)));

        var stored = pipeline.Alerts.Rows.Select(a => a.StartTs).Order().ToList();
        for (var i = 1; i < stored.Count; i++)
            Assert.True(stored[i] - stored[i - 1] >= TimeSpan.FromMinutes(5), $"{stored[i - 1]:T} and {stored[i]:T}");
    }

    [Fact]
    public async Task The_alerts_endpoint_lists_them_and_filters()
    {
        var pipeline = new Pipeline(
            TestCatalog.Rule("a", "temperature", "SustainedAbove", Sustained),
            TestCatalog.Rule("b", "vibration", "SustainedAbove", Sustained));

        await pipeline.RunAsync(Episode(0).Concat(Episode(0, sensor: "PUMP-02")));

        Assert.Equal(2, (await pipeline.AlertQueries.GetAsync(null, null, null, null, null, 1, 100)).Data!.Count);
        Assert.Single((await pipeline.AlertQueries.GetAsync("pump-02", null, null, null, null, 1, 100)).Data!);
        Assert.Empty((await pipeline.AlertQueries.GetAsync(null, null, "nope", null, null, 1, 100)).Data!);
        Assert.Equal(404, (await pipeline.AlertQueries.GetAsync("GHOST", null, null, null, null, 1, 100)).StatusCode);
        Assert.Equal(400, (await pipeline.AlertQueries.GetAsync(null, null, null, null, null, 0, 100)).StatusCode);
    }
}
