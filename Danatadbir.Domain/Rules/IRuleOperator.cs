namespace Danatadbir.Domain.Rules;

public interface IRuleOperator
{
    string Key { get; }

    Type ParametersType { get; }
}
