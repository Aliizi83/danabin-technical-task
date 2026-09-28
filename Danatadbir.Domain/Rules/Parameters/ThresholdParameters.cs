namespace Danatadbir.Domain.Rules.Parameters;

public record ThresholdParameters : IOperatorParameters
{
    public double? Threshold { get; init; }

    public IReadOnlyList<string> Validate() =>
        Threshold is null
            ? ["threshold is required"]
            : double.IsFinite(Threshold.Value)
                ? []
                : ["threshold must be a finite number"];

    public double Value => Threshold!.Value;
}
