namespace Danatadbir.Domain.Rules.Parameters;

public record EqualParameters : IOperatorParameters
{
    public double? Value { get; init; }

    public double Tolerance { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Value is null)
            errors.Add("value is required");
        else if (!double.IsFinite(Value.Value))
            errors.Add("value must be a finite number");

        if (!double.IsFinite(Tolerance) || Tolerance < 0)
            errors.Add("tolerance must be a finite number that is not negative");

        return errors;
    }

    public double Target => Value!.Value;
}
