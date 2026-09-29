using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Tests.Fakes;
using Microsoft.Extensions.Logging;
using static Danatadbir.Tests.Fakes.Pipeline;

namespace Danatadbir.Tests.Ingestion;

public class IngestionServiceTests
{
    // ------------------------------------------------------------ counting and rejection

    [Fact]
    public async Task Every_kind_of_bad_line_is_counted_by_its_own_reason_and_never_stored()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(
            Line(0, 70),                                                             // good
            "{bad json",                                                             // malformed
            """{"deviceId":"PUMP-01","metric":"temperature","ts":"2030-01-01T00:00:01Z","value":"abc","seq":9}""", // malformed
            """{"deviceId":null,"metric":"temperature","ts":"2030-01-01T00:00:02Z","value":1,"seq":9}""",          // invalid
            """{"deviceId":"PUMP-01","metric":"temperature","ts":"2030-02-30T00:00:00Z","value":1,"seq":9}""",     // invalid
            Line(5, 70, sensor: "GHOST-9"),                                          // unknown sensor
            Line(6, 70, metric: "humidity"),                                         // unknown metric
            Line(0, 70));                                                            // duplicate

        Assert.Equal(8, report.TotalLinesRead);
        Assert.Equal(1, report.StoredReadings);
        Assert.Equal(2, report.MalformedLines);
        Assert.Equal(2, report.InvalidRecords);
        Assert.Equal(2, report.UnknownSensorOrMetric);
        Assert.Equal(1, report.DuplicatesRemoved);
        Assert.Single(pipeline.Readings.Points);
    }

    [Fact]
    public async Task Rejected_and_duplicate_readings_are_never_counted_as_unacceptable()
    {
        var pipeline = new Pipeline();

        // Values that would violate temp-high if they were evaluated.
        var report = await pipeline.RunAsync(
            "{bad", Line(1, 500, sensor: "GHOST"), Line(2, 500, metric: "humidity"),
            """{"deviceId":"","metric":"temperature","ts":"2030-01-01T00:00:03Z","value":500,"seq":9}""");

        Assert.Equal(0, report.UnacceptableReadings);
        Assert.Equal(0, report.RuleViolations);
        Assert.Empty(pipeline.RuleResults.Rows);
    }

    [Fact]
    public async Task Blank_lines_are_skipped_and_not_counted()
    {
        var report = await new Pipeline().RunAsync(Line(0, 70), "", "   ", Line(10, 71));

        Assert.Equal(2, report.TotalLinesRead);
        Assert.Equal(2, report.StoredReadings);
    }

    [Fact]
    public async Task A_missing_feed_is_reported_not_thrown()
    {
        var result = await new Pipeline().Ingestion.IngestAsync("missing");

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task An_empty_feed_is_a_valid_run_with_nothing_in_it()
    {
        var report = await new Pipeline().RunAsync();

        Assert.Equal(0, report.TotalLinesRead);
        Assert.Equal(0, report.StoredReadings);
    }

    [Fact]
    public async Task Rejection_samples_say_what_was_wrong_and_where()
    {
        var report = await new Pipeline().RunAsync(Line(0, 70), "{bad json");

        var sample = Assert.Single(report.RejectionSamples);
        Assert.Equal(2, sample.LineNumber);
        Assert.Equal(RejectionReason.Malformed, sample.Reason);
    }

    [Fact]
    public async Task Every_bad_line_is_logged_as_a_warning_with_its_line_number_and_reason()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 70), "{bad json", Line(1, 70, sensor: "GHOST"));

        var warnings = pipeline.IngestionLog.Entries.Where(e => e.Level == LogLevel.Warning).Select(e => e.Message).ToList();
        Assert.Contains(warnings, m => m.Contains("Line 2") && m.Contains("Malformed"));
        Assert.Contains(warnings, m => m.Contains("Line 3") && m.Contains("UnknownSensorOrMetric"));
    }

    [Fact]
    public async Task A_duplicate_is_not_a_warning()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 70), Line(0, 70));

        Assert.DoesNotContain(pipeline.IngestionLog.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task A_badly_broken_file_cannot_flood_the_log()
    {
        var pipeline = new Pipeline();   // sample limit is 50

        await pipeline.RunAsync(Enumerable.Range(0, 500).Select(i => $"{{broken {i}"));

        var individually = pipeline.IngestionLog.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("rejected as"));
        Assert.Equal(50, individually);
        Assert.Contains(pipeline.IngestionLog.Entries, e => e.Message.Contains("450 further rejected"));
    }

    [Fact]
    public async Task The_processing_report_is_logged_with_the_counts_the_task_lists()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 95), Line(10, 70), "{bad");

        var report = pipeline.IngestionLog.Entries.Single(e => e.Message.StartsWith("Processing report")).Message;
        foreach (var label in new[]
                 {
                     "total lines read", "parsed readings", "stored readings", "duplicates removed", "invalid records rejected",
                     "rules loaded", "rule evaluations performed", "acceptable readings", "unacceptable readings",
                     "rule violations", "alerts generated"
                 })
        {
            Assert.Contains(label, report);
        }
    }

    [Fact]
    public async Task Sustained_episodes_and_raised_alerts_are_logged()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Enumerable.Range(0, 8).Select(i => Line(i * 10, 75)));

        Assert.Contains(pipeline.IngestionLog.Entries, e => e.Message.StartsWith("Sustained episode"));
        Assert.Contains(pipeline.AlertingLog.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("Alert raised"));
    }

    [Fact]
    public async Task A_suppressed_episode_is_logged_with_the_alert_that_covered_it()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(
            Enumerable.Range(0, 8).Select(i => Line(i * 10, 75)).Append(Line(80, 60))
                .Concat(Enumerable.Range(0, 8).Select(i => Line(220 + i * 10, 75))));

        Assert.Contains(pipeline.AlertingLog.Entries, e => e.Message.StartsWith("Episode suppressed by cooldown"));
    }

    // ------------------------------------------------------------ deduplication

    [Fact]
    public async Task A_duplicate_is_counted_once_per_extra_occurrence()
    {
        var report = await new Pipeline().RunAsync(Line(0, 70), Line(0, 70), Line(0, 70));

        Assert.Equal(2, report.DuplicatesRemoved);
        Assert.Equal(1, report.StoredReadings);
        Assert.Equal(3, report.ParsedReadings);
    }

    [Fact]
    public async Task Deduplication_keeps_the_last_occurrence()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Line(0, 10, seq: 5), Line(0, 20, seq: 5), Line(0, 30, seq: 5));

        Assert.Equal(30, Assert.Single(pipeline.Readings.Points.Values).Value);
    }

    [Fact]
    public async Task The_surviving_duplicate_is_the_one_that_gets_evaluated()
    {
        var pipeline = new Pipeline();

        // The first copy would violate temp-high; the last copy, which wins, does not.
        var withoutViolation = await pipeline.RunAsync(Line(0, 500, seq: 5), Line(0, 50, seq: 5));
        Assert.Equal(0, withoutViolation.RuleViolations);
        Assert.Empty(pipeline.RuleResults.Rows);

        // And the other way round.
        var other = new Pipeline();
        var withViolation = await other.RunAsync(Line(0, 50, seq: 5), Line(0, 500, seq: 5));
        Assert.Equal(1, withViolation.RuleViolations);
        Assert.Single(other.RuleResults.Rows);
    }

    [Fact]
    public async Task Readings_sharing_a_timestamp_but_not_a_seq_are_distinct()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(Line(0, 60, seq: 1), Line(0, 61, seq: 2));

        Assert.Equal(0, report.DuplicatesRemoved);
        Assert.Equal(2, pipeline.Readings.Points.Count);
    }

    [Fact]
    public async Task Readings_differing_only_in_sensor_or_metric_are_distinct()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(
            Line(0, 60, seq: 1), Line(0, 60, seq: 1, sensor: "PUMP-02"), Line(0, 60, seq: 1, metric: "pressure"));

        Assert.Equal(0, report.DuplicatesRemoved);
        Assert.Equal(3, pipeline.Readings.Points.Count);
    }

    [Fact]
    public async Task Sensor_and_metric_spelling_is_resolved_to_the_registered_form()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(Line(0, 60, seq: 1, sensor: "pump-01", metric: "TEMPERATURE"), Line(0, 90, seq: 1));

        Assert.Equal(1, report.DuplicatesRemoved);
        var stored = Assert.Single(pipeline.Readings.Points.Values);
        Assert.Equal("PUMP-01", stored.SensorExternalId);
        Assert.Equal("temperature", stored.MetricKey);
        Assert.Equal(90, stored.Value);
    }

    // ------------------------------------------------------------ evaluation and classification

    [Fact]
    public async Task A_violating_reading_is_unacceptable_with_the_rule_and_a_reason()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(Line(0, 95), Line(10, 70));

        Assert.Equal(1, report.UnacceptableReadings);
        Assert.Equal(1, report.AcceptableReadings);
        var row = Assert.Single(pipeline.RuleResults.Rows);
        Assert.Equal("temp-high", row.RuleId);
        Assert.Contains("95", row.Reason);
    }

    [Fact]
    public async Task A_reading_breaking_two_rules_is_one_unacceptable_reading_with_two_violations()
    {
        var pipeline = new Pipeline(
            TestCatalog.Rule("a", "temperature", "GreaterThan", """{"threshold":50}"""),
            TestCatalog.Rule("b", "temperature", "GreaterThan", """{"threshold":60}"""));

        var report = await pipeline.RunAsync(Line(0, 70));

        Assert.Equal(1, report.UnacceptableReadings);
        Assert.Equal(2, report.RuleViolations);
        Assert.Equal(2, report.RuleEvaluationsPerformed);
    }

    [Fact]
    public async Task A_reading_no_enabled_rule_applies_to_is_acceptable()
    {
        var report = await new Pipeline().RunAsync(Line(0, 9999, metric: "pressure")); // the only pressure rule is disabled

        Assert.Equal(1, report.AcceptableReadings);
        Assert.Equal(0, report.RuleEvaluationsPerformed);
    }

    [Fact]
    public async Task Only_applicable_rules_count_as_evaluations()
    {
        var report = await new Pipeline().RunAsync(
            Line(0, 70),                       // temp-high applies
            Line(0, 1, metric: "vibration"),   // vibration-negative applies
            Line(0, 1, metric: "pressure"));   // nothing enabled applies

        Assert.Equal(2, report.RuleEvaluationsPerformed);
    }

    [Fact]
    public async Task Readings_inside_a_sustained_episode_stay_acceptable()
    {
        var pipeline = new Pipeline();

        var report = await pipeline.RunAsync(Enumerable.Range(0, 7).Select(i => Line(i * 10, 75)));

        Assert.Equal(1, report.SustainedEpisodes);
        Assert.Equal(7, report.AcceptableReadings);
        Assert.Equal(0, report.UnacceptableReadings);
    }

    [Fact]
    public async Task Acceptable_and_unacceptable_readings_add_up_to_the_stored_ones()
    {
        var report = await new Pipeline().RunAsync(
            Line(0, 95), Line(10, 70), Line(20, 500), Line(30, -1, metric: "vibration"), Line(40, 1, metric: "pressure"), Line(0, 95));

        Assert.Equal(report.StoredReadings, report.AcceptableReadings + report.UnacceptableReadings);
    }

    [Fact]
    public async Task Every_batch_is_written_when_the_feed_is_longer_than_one_batch()
    {
        var pipeline = new Pipeline();

        await pipeline.RunAsync(Enumerable.Range(0, 10).Select(i => Line(i * 10, 60)));

        Assert.Equal(10, pipeline.Readings.Points.Count);
        Assert.True(pipeline.Readings.WriteCalls >= 4);   // batch size is 3
    }

    // ------------------------------------------------------------ order

    private static IEnumerable<string> MixedFeed() =>
        Enumerable.Range(0, 20).Select(i => Line(i * 10, i % 5 == 0 ? 95 : 60 + i))
            .Concat(Enumerable.Range(0, 8).Select(i => Line(i * 10, 75, sensor: "PUMP-02")))
            .Concat(Enumerable.Range(0, 9).Select(i => Line(300 + i * 10, 75)))
            .Concat([Line(50, 95), "{bad", Line(0, 500, sensor: "GHOST")]);   // an identical duplicate

    [Fact]
    public async Task Among_conflicting_duplicates_the_last_line_of_the_file_wins()
    {
        // Last-wins is a rule about file order, so this is the one place order is allowed to matter.
        var forward = new Pipeline();
        var backward = new Pipeline();

        await forward.RunAsync(Line(0, 10, seq: 5), Line(0, 20, seq: 5));
        await backward.RunAsync(Line(0, 20, seq: 5), Line(0, 10, seq: 5));

        Assert.Equal(20, Assert.Single(forward.Readings.Points.Values).Value);
        Assert.Equal(10, Assert.Single(backward.Readings.Points.Values).Value);
    }

    [Fact]
    public async Task The_outcome_does_not_depend_on_the_order_of_the_file()
    {
        var feed = MixedFeed().ToList();
        var orderings = new[] { feed, Enumerable.Reverse(feed).ToList(), feed.OrderBy(l => l.GetHashCode() * 31).ToList() };

        var outcomes = new List<(IngestionReportDto Report, string Stored, string Violations, string Alerts)>();

        foreach (var lines in orderings)
        {
            var pipeline = new Pipeline();
            var report = await pipeline.RunAsync(lines);
            outcomes.Add((report,
                string.Join(";", pipeline.Readings.Points.Values.OrderBy(r => r.Identity).Select(r => $"{r.Identity}={r.Value}")),
                string.Join(";", pipeline.RuleResults.Rows.OrderBy(r => r.RuleId).ThenBy(r => r.Timestamp).Select(r => $"{r.RuleId}@{r.Timestamp:o}")),
                string.Join(";", pipeline.Alerts.Rows.OrderBy(a => a.StartTs).Select(a => $"{a.RuleId}@{a.StartTs:o}"))));
        }

        foreach (var outcome in outcomes.Skip(1))
        {
            Assert.Equal(outcomes[0].Stored, outcome.Stored);
            Assert.Equal(outcomes[0].Violations, outcome.Violations);
            Assert.Equal(outcomes[0].Alerts, outcome.Alerts);
            Assert.Equal(outcomes[0].Report.AcceptableReadings, outcome.Report.AcceptableReadings);
            Assert.Equal(outcomes[0].Report.UnacceptableReadings, outcome.Report.UnacceptableReadings);
            Assert.Equal(outcomes[0].Report.SustainedEpisodes, outcome.Report.SustainedEpisodes);
        }
    }
}
