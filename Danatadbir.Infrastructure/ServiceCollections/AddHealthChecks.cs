using Danatadbir.Infrastructure.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class AddHealthChecks
{
    public static IServiceCollection AddInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
            .AddCheck<InfluxDbHealthCheck>("influxdb", tags: ["ready"]);

        return services;
    }
}
