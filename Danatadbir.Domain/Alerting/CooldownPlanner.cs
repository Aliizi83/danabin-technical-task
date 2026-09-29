using Danatadbir.Domain.Rules;

namespace Danatadbir.Domain.Alerting;

public enum AlertDecision
{
    /// <summary>No alert is near enough: a new alert is raised.</summary>
    Raise,

    /// <summary>An alert for exactly this episode already exists, from an earlier run.</summary>
    AlreadyRaised,

    /// <summary>Another alert lies within the cooldown window, so this episode raises none.</summary>
    Suppressed
}

public record EpisodeCandidate(AlertSeriesKey Key, string RuleName, TimeSpan Cooldown, ViolationEpisode Episode);

/// <param name="SuppressedBy">Start of the alert that suppressed the episode, when it was suppressed.</param>
public record PlannedAlert(EpisodeCandidate Candidate, AlertDecision Decision, DateTime? SuppressedBy = null);

/// <summary>
/// Decides which sustained episodes become alerts. Pure: it reads no store and writes none, so the
/// policy can be checked on its own.
///
/// <para>The window is <b>symmetric</b>. An episode is suppressed when an alert of the same rule,
/// sensor and metric starts less than the cooldown before <i>or after</i> it. Looking only
/// backwards would let a late-arriving episode land three minutes before an alert that is already
/// stored, and two alerts closer than the cooldown would exist. Looking both ways keeps one
/// invariant: no two alerts of a key are nearer than its cooldown.</para>
///
/// <para>Within a run episodes are taken in event-time order, so the earlier of two close episodes
/// wins. Against alerts stored by earlier runs the result depends on arrival order — that is the
/// price of never retracting an alert that has already been raised.</para>
///
/// <para>The cooldown is counted from the start of the previous alert. A start never moves once
/// detected, whereas an episode still open at the end of a feed has an end that does.</para>
/// </summary>
public static class CooldownPlanner
{
    public static IReadOnlyList<PlannedAlert> Plan(
        IEnumerable<EpisodeCandidate> candidates,
        IReadOnlyDictionary<AlertSeriesKey, IReadOnlyList<DateTime>> existingStarts)
    {
        var plan = new List<PlannedAlert>();

        foreach (var group in candidates.GroupBy(candidate => candidate.Key))
        {
            var starts = existingStarts.TryGetValue(group.Key, out var known) ? known.ToList() : [];

            foreach (var candidate in group.OrderBy(candidate => candidate.Episode.StartTs))
            {
                var start = candidate.Episode.StartTs;

                if (starts.Contains(start))
                {
                    plan.Add(new PlannedAlert(candidate, AlertDecision.AlreadyRaised));
                    continue;
                }

                var nearest = starts
                    .Select(existing => (Start: existing, Distance: (start - existing).Duration()))
                    .Where(pair => pair.Distance < candidate.Cooldown)
                    .OrderBy(pair => pair.Distance)
                    .Select(pair => (DateTime?)pair.Start)
                    .FirstOrDefault();

                if (nearest is not null)
                {
                    plan.Add(new PlannedAlert(candidate, AlertDecision.Suppressed, nearest));
                    continue;
                }

                starts.Add(start);
                plan.Add(new PlannedAlert(candidate, AlertDecision.Raise));
            }
        }

        return plan
            .OrderBy(planned => planned.Candidate.Episode.StartTs)
            .ThenBy(planned => planned.Candidate.Key.RuleId)
            .ThenBy(planned => planned.Candidate.Key.SensorExternalId)
            .ToList();
    }
}
