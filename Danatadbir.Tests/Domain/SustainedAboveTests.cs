using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules;
using Danatadbir.Domain.Rules.Operators;
using Danatadbir.Domain.Rules.Parameters;
using Danatadbir.Tests.Fakes;

namespace Danatadbir.Tests.Domain;

public class SustainedAboveTests
{
    private const double Above = 71;
    private const double Below = 70;

    private static readonly SustainedAboveParameters Sixty = new() { Threshold = 70.5, DurationSeconds = 60 };

    private static IReadOnlyList<ViolationEpisode> Detect(
        IEnumerable<SensorData> series, SustainedAboveParameters? parameters = null) =>
        new SustainedAboveOperator().Detect(series.ToList(), parameters ?? Sixty);

    [Fact]
    public void A_stretch_that_lasts_the_duration_is_an_episode()
    {
        var episode = Assert.Single(Detect(TestData.Run(0, 60, Above)));

        Assert.Equal(TestData.At(0), episode.StartTs);
        Assert.Equal(TestData.At(60), episode.EndTs);
        Assert.Equal(7, episode.ReadingCount);
        Assert.Equal(Above, episode.PeakValue);
    }

    [Fact]
    public void A_stretch_shorter_than_the_duration_is_not()
    {
        Assert.Empty(Detect(TestData.Run(0, 50, Above)));
    }

    [Fact]
    public void A_lone_spike_never_qualifies()
    {
        Assert.Empty(Detect([TestData.Reading(0, 500)]));
    }

    [Fact]
    public void A_stretch_that_is_still_open_ends_at_the_last_observed_reading()
    {
        var episode = Assert.Single(Detect(TestData.Run(0, 80, Above)));

        Assert.Equal(TestData.At(80), episode.EndTs);
    }

    [Fact]
    public void The_episode_ends_at_the_last_reading_still_above_not_at_the_drop()
    {
        var series = TestData.Run(0, 60, Above).Append(TestData.Reading(70, Below)).ToList();

        var episode = Assert.Single(Detect(series));

        Assert.Equal(TestData.At(60), episode.EndTs);
    }

    [Fact]
    public void A_reading_exactly_at_the_threshold_breaks_the_stretch()
    {
        var series = TestData.Run(0, 30, Above)
            .Append(TestData.Reading(40, 70.5))
            .Concat(TestData.Run(50, 80, Above))
            .ToList();

        Assert.Empty(Detect(series));
    }

    [Fact]
    public void Out_of_order_readings_give_the_same_result_as_sorted_ones()
    {
        var sorted = TestData.Run(0, 60, Above)
            .Append(TestData.Reading(70, Below))
            .Concat(TestData.Run(80, 150, Above))
            .ToList();

        var shuffled = sorted.OrderBy(reading => reading.Seq * 7919 % 13).ToList();
        var reversed = Enumerable.Reverse(sorted).ToList();

        Assert.NotEqual(sorted, shuffled);
        Assert.Equal(Detect(sorted), Detect(shuffled));
        Assert.Equal(Detect(sorted), Detect(reversed));
        Assert.Equal(2, Detect(shuffled).Count);
    }

    [Fact]
    public void Ordering_follows_event_time_not_the_sequence_number()
    {
        // seq decreases as time advances, so sorting by seq would reverse the series.
        var series = TestData.Run(0, 60, Above)
            .Select(reading => reading with { Seq = 1000 - (long)(reading.Timestamp - TestData.Origin).TotalSeconds })
            .Append(TestData.Reading(70, Below, seq: 5))
            .ToList();

        var episode = Assert.Single(Detect(series));

        Assert.Equal(TestData.At(0), episode.StartTs);
        Assert.Equal(TestData.At(60), episode.EndTs);
    }

    [Fact]
    public void A_late_reading_that_arrives_last_but_falls_inside_the_stretch_still_counts()
    {
        // The 30 s reading is dated inside the stretch but is the last line of the file.
        var series = TestData.Run(0, 60, Above).Where(reading => reading.Timestamp != TestData.At(30)).ToList();
        series.Add(TestData.Reading(30, Above));

        var episode = Assert.Single(Detect(series));

        Assert.Equal(7, episode.ReadingCount);
    }

    [Fact]
    public void A_late_reading_below_the_threshold_splits_the_stretch()
    {
        var series = TestData.Run(0, 60, Above).Where(reading => reading.Timestamp != TestData.At(30)).ToList();
        series.Add(TestData.Reading(30, Below));

        Assert.Empty(Detect(series));
    }

    [Fact]
    public void Separate_stretches_give_separate_episodes()
    {
        var series = TestData.Run(0, 60, Above)
            .Append(TestData.Reading(70, Below))
            .Concat(TestData.Run(80, 140, Above))
            .ToList();

        var episodes = Detect(series);

        Assert.Equal(2, episodes.Count);
        Assert.Equal(TestData.At(80), episodes[1].StartTs);
    }

    [Fact]
    public void The_peak_is_the_highest_value_inside_the_episode()
    {
        var series = TestData.Run(0, 60, Above).ToList();
        series[3] = series[3] with { Value = 74.2 };

        Assert.Equal(74.2, Assert.Single(Detect(series)).PeakValue);
    }

    [Fact]
    public void Readings_at_the_same_timestamp_are_ordered_by_seq()
    {
        // Above then below at t=60 keeps the stretch (60 s); below then above ends it at 50 s.
        var aboveFirst = TestData.Run(0, 50, Above)
            .Append(TestData.Reading(60, Above, seq: 1)).Append(TestData.Reading(60, Below, seq: 2)).ToList();
        var belowFirst = TestData.Run(0, 50, Above)
            .Append(TestData.Reading(60, Below, seq: 1)).Append(TestData.Reading(60, Above, seq: 2)).ToList();

        Assert.Single(Detect(aboveFirst));
        Assert.Empty(Detect(belowFirst));
    }

    [Fact]
    public void An_empty_series_has_no_episodes() => Assert.Empty(Detect([]));

    [Fact]
    public void A_silence_longer_than_maxGap_breaks_the_stretch()
    {
        var gapped = new SustainedAboveParameters { Threshold = 70.5, DurationSeconds = 60, MaxGapSeconds = 30 };
        var series = new[] { 0, 10, 20, 300, 310 }.Select(s => TestData.Reading(s, Above)).ToList();

        Assert.Empty(Detect(series, gapped));
    }

    [Fact]
    public void A_silence_of_exactly_maxGap_is_tolerated()
    {
        var gapped = new SustainedAboveParameters { Threshold = 70.5, DurationSeconds = 60, MaxGapSeconds = 30 };
        var series = new[] { 0, 10, 20, 30, 40, 50, 80, 90, 100, 110, 120 }.Select(s => TestData.Reading(s, Above)).ToList();

        var episode = Assert.Single(Detect(series, gapped));

        Assert.Equal(TestData.At(120), episode.EndTs);
    }

    [Fact]
    public void Without_maxGap_any_silence_is_bridged()
    {
        var series = new[] { 0, 10, 20, 300, 310 }.Select(s => TestData.Reading(s, Above)).ToList();

        Assert.Single(Detect(series));
    }
}
