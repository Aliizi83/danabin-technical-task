using Danatadbir.Domain.Entities;
using Danatadbir.Domain.Rules.Parameters;

namespace Danatadbir.Domain.Rules.Operators;

public class SustainedAboveOperator : SeriesRuleOperator<SustainedAboveParameters>
{
    public override string Key => "SustainedAbove";

    protected override IReadOnlyList<ViolationEpisode> Detect(
        IReadOnlyList<SensorData> series,
        SustainedAboveParameters parameters)
    {
        var episodes = new List<ViolationEpisode>();

        if (series.Count == 0)
            return episodes;

        var ordered = series
            .OrderBy(reading => reading.Timestamp)
            .ThenBy(reading => reading.Seq)
            .ToList();

        DateTime? start = null;
        DateTime lastAbove = default;
        var peak = double.NegativeInfinity;
        var count = 0;

        foreach (var reading in ordered)
        {
            if (reading.Value > parameters.Limit)
            {
                start ??= reading.Timestamp;
                lastAbove = reading.Timestamp;
                peak = Math.Max(peak, reading.Value);
                count++;
                continue;
            }

            AddIfQualified(episodes, start, lastAbove, peak, count, parameters);
            start = null;
            peak = double.NegativeInfinity;
            count = 0;
        }

        AddIfQualified(episodes, start, lastAbove, peak, count, parameters);

        return episodes;
    }

    private static void AddIfQualified(
        List<ViolationEpisode> episodes,
        DateTime? start,
        DateTime lastAbove,
        double peak,
        int count,
        SustainedAboveParameters parameters)
    {
        if (start is null)
            return;

        if (lastAbove - start.Value < parameters.MinimumDuration)
            return;

        episodes.Add(new ViolationEpisode(start.Value, lastAbove, peak, count));
    }
}
