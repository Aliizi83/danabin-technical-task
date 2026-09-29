namespace Danatadbir.Domain.Rules.Parameters;

public record BetweenParameters : IOperatorParameters
{
    public double? Min { get; init; }

    public double? Max { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Min is null)
            errors.Add("min is required");
        else if (!double.IsFinite(Min.Value))
            errors.Add("min must be a finite number");

        if (Max is null)
            errors.Add("max is required");
        else if (!double.IsFinite(Max.Value))
            errors.Add("max must be a finite number");

        if (Min is not null && Max is not null && Min > Max)
            errors.Add($"min ({Min}) must not be greater than max ({Max})");

        return errors;
    }

    public double LowerBound => Min!.Value;

    public double UpperBound => Max!.Value;
}
