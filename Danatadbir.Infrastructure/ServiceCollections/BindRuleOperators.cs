using Danatadbir.Domain.Rules;
using Danatadbir.Domain.Rules.Operators;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class BindRuleOperators
{
    public static IServiceCollection BindOperators(this IServiceCollection services)
    {
        services.AddSingleton<IRuleOperator, GreaterThanOperator>();
        services.AddSingleton<IRuleOperator, GreaterThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, LessThanOperator>();
        services.AddSingleton<IRuleOperator, LessThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, EqualOperator>();
        services.AddSingleton<IRuleOperator, BetweenOperator>();

        return services;
    }
}
