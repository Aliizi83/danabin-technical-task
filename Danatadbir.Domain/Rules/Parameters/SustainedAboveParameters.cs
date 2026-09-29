namespace Danatadbir.Domain.Rules.Parameters;

/// <summary>
/// Parameters of the stateful operator: how high, and for how long. Unlike the stateless
/// parameters these describe a window, not a point.
/// </summary>
public record SustainedAboveParameters : IOperatorParameters
{
    public double? Threshold { get; init; }

    /// <summary>Minimum length of the above-threshold stretch, measured in event time.</summary>
    public int? DurationSeconds { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Threshold is null)
            errors.Add("threshold is required");
        else if (!double.IsFinite(Threshold.Value))
            errors.Add("threshold must be a finite number");

        if (DurationSeconds is null)
            errors.Add("durationSeconds is required");
        else if (DurationSeconds <= 0)
            errors.Add("durationSeconds must be greater than zero");

        return errors;
    }

    public double Limit => Threshold!.Value;

    public TimeSpan MinimumDuration => TimeSpan.FromSeconds(DurationSeconds!.Value);
}
