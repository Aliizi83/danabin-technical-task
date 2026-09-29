using Danatadbir.Domain.Alerting;
using Danatadbir.Domain.Rules;
using Danatadbir.Tests.Fakes;

namespace Danatadbir.Tests.Alerting;

public class CooldownPlannerTests
{
    private static readonly TimeSpan Five = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<AlertSeriesKey, IReadOnlyList<DateTime>> NoAlerts = [];

    private static AlertSeriesKey Key(string rule = "r1", string sensor = "PUMP-01") => new(rule, sensor, "temperature");

    private static EpisodeCandidate Episode(double startSeconds, string rule = "r1", string sensor = "PUMP-01", TimeSpan? cooldown = null) =>
        new(Key(rule, sensor), rule, cooldown ?? Five,
            new ViolationEpisode(TestData.At(startSeconds), TestData.At(startSeconds + 60), 72, 7));

    private static Dictionary<AlertSeriesKey, IReadOnlyList<DateTime>> Existing(AlertSeriesKey key, params double[] startSeconds) =>
        new() { [key] = startSeconds.Select(TestData.At).ToList() };

    private static AlertDecision[] Decisions(IEnumerable<EpisodeCandidate> candidates,
        IReadOnlyDictionary<AlertSeriesKey, IReadOnlyList<DateTime>>? existing = null) =>
        CooldownPlanner.Plan(candidates, existing ?? NoAlerts).Select(p => p.Decision).ToArray();

    [Fact]
    public void A_single_episode_raises_an_alert() =>
        Assert.Equal([AlertDecision.Raise], Decisions([Episode(0)]));

    [Fact]
    public void An_episode_inside_the_cooldown_of_the_previous_alert_is_suppressed() =>
        Assert.Equal([AlertDecision.Raise, AlertDecision.Suppressed], Decisions([Episode(0), Episode(220)]));

    [Fact]
    public void The_cooldown_is_counted_from_the_start_of_the_previous_alert()
    {
        // The first episode lasts 60 s, so the second starts 160 s after its end but only 220 s after its start.
        var plan = CooldownPlanner.Plan([Episode(0), Episode(220)], NoAlerts);

        Assert.Equal(AlertDecision.Suppressed, plan[1].Decision);
        Assert.Equal(TestData.At(0), plan[1].SuppressedBy);
    }

    [Fact]
    public void An_episode_exactly_one_cooldown_later_raises_again() =>
        Assert.Equal([AlertDecision.Raise, AlertDecision.Raise], Decisions([Episode(0), Episode(300)]));

    [Fact]
    public void An_episode_just_before_the_cooldown_ends_is_suppressed() =>
        Assert.Equal([AlertDecision.Raise, AlertDecision.Suppressed], Decisions([Episode(0), Episode(299)]));

    [Fact]
    public void Episodes_separated_by_more_than_the_cooldown_raise_separate_alerts() =>
        Assert.Equal([AlertDecision.Raise, AlertDecision.Raise, AlertDecision.Raise], Decisions([Episode(0), Episode(400), Episode(800)]));

    [Fact]
    public void One_alert_covers_a_burst_of_close_episodes() =>
        Assert.Equal(
            [AlertDecision.Raise, AlertDecision.Suppressed, AlertDecision.Suppressed],
            Decisions([Episode(0), Episode(100), Episode(200)]));

    [Fact]
    public void Suppression_does_not_extend_the_cooldown()
    {
        // 0 raises; 200 is suppressed and must not restart the clock, so 320 (300 s after 0) raises.
        Assert.Equal(
            [AlertDecision.Raise, AlertDecision.Suppressed, AlertDecision.Raise],
            Decisions([Episode(0), Episode(200), Episode(320)]));
    }

    [Fact]
    public void Episodes_are_taken_in_event_time_order_whatever_order_they_arrive_in() =>
        Assert.Equal(
            Decisions([Episode(0), Episode(220)]),
            Decisions([Episode(220), Episode(0)]));

    [Fact]
    public void The_key_includes_the_sensor_so_one_sensor_does_not_silence_another() =>
        Assert.Equal(
            [AlertDecision.Raise, AlertDecision.Raise],
            Decisions([Episode(0, sensor: "PUMP-01"), Episode(60, sensor: "PUMP-02")]));

    [Fact]
    public void The_key_includes_the_rule_so_one_rule_does_not_silence_another() =>
        Assert.Equal(
            [AlertDecision.Raise, AlertDecision.Raise],
            Decisions([Episode(0, rule: "r1"), Episode(60, rule: "r2")]));

    [Fact]
    public void Each_rule_uses_its_own_cooldown()
    {
        var short_ = TimeSpan.FromSeconds(60);

        Assert.Equal([AlertDecision.Raise, AlertDecision.Raise], Decisions([Episode(0, cooldown: short_), Episode(100, cooldown: short_)]));
        Assert.Equal([AlertDecision.Raise, AlertDecision.Suppressed], Decisions([Episode(0), Episode(100)]));
    }

    [Fact]
    public void A_zero_cooldown_raises_every_episode() =>
        Assert.Equal(
            [AlertDecision.Raise, AlertDecision.Raise],
            Decisions([Episode(0, cooldown: TimeSpan.Zero), Episode(1, cooldown: TimeSpan.Zero)]));

    // ---- alerts stored by earlier runs ----

    [Fact]
    public void An_episode_inside_the_cooldown_of_a_stored_alert_is_suppressed() =>
        Assert.Equal([AlertDecision.Suppressed], Decisions([Episode(200)], Existing(Key(), 0)));

    [Fact]
    public void An_episode_far_from_any_stored_alert_raises() =>
        Assert.Equal([AlertDecision.Raise], Decisions([Episode(1000)], Existing(Key(), 0)));

    [Fact]
    public void An_episode_whose_alert_is_already_stored_is_recognised_not_suppressed()
    {
        var plan = CooldownPlanner.Plan([Episode(0)], Existing(Key(), 0));

        Assert.Equal(AlertDecision.AlreadyRaised, plan[0].Decision);
    }

    [Fact]
    public void A_late_episode_is_compared_with_the_alert_before_it_not_the_newest_one()
    {
        // Stored alerts at 3600 and 7200. An episode at 300 is an hour before the nearest one.
        Assert.Equal([AlertDecision.Raise], Decisions([Episode(300)], Existing(Key(), 3600, 7200)));
    }

    [Fact]
    public void An_older_episode_just_before_a_stored_alert_is_suppressed_by_it()
    {
        // 180 s before the stored alert: two alerts closer than the cooldown must not exist.
        var plan = CooldownPlanner.Plan([Episode(0)], Existing(Key(), 180));

        Assert.Equal(AlertDecision.Suppressed, plan[0].Decision);
        Assert.Equal(TestData.At(180), plan[0].SuppressedBy);
    }

    [Fact]
    public void Planning_the_same_episodes_after_they_were_stored_raises_nothing_new()
    {
        var episodes = new[] { Episode(0), Episode(220), Episode(400), Episode(1000) };

        var first = CooldownPlanner.Plan(episodes, NoAlerts);
        var stored = first.Where(p => p.Decision == AlertDecision.Raise).Select(p => p.Candidate.Episode.StartTs).ToList();
        var second = CooldownPlanner.Plan(episodes, new Dictionary<AlertSeriesKey, IReadOnlyList<DateTime>> { [Key()] = stored });

        Assert.DoesNotContain(second, p => p.Decision == AlertDecision.Raise);
        Assert.Equal(first.Count(p => p.Decision == AlertDecision.Raise), second.Count(p => p.Decision == AlertDecision.AlreadyRaised));
    }

    [Fact]
    public void No_two_raised_alerts_of_a_key_are_ever_nearer_than_the_cooldown()
    {
        var random = new Random(11);
        var episodes = Enumerable.Range(0, 200).Select(_ => Episode(random.Next(0, 5000))).DistinctBy(e => e.Episode.StartTs).ToList();

        var raised = CooldownPlanner.Plan(episodes, NoAlerts)
            .Where(p => p.Decision == AlertDecision.Raise).Select(p => p.Candidate.Episode.StartTs).Order().ToList();

        for (var i = 1; i < raised.Count; i++)
            Assert.True(raised[i] - raised[i - 1] >= Five, $"{raised[i - 1]:T} and {raised[i]:T} are too close");
    }
}
