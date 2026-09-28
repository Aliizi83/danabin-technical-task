using Danatadbir.Domain.Repositories;
using Danatadbir.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class BindRepositories
{
    public static IServiceCollection BindDomainRepositories(this IServiceCollection services)
    {
        services.AddScoped<ISensorRepository, SensorRepository>();
        services.AddScoped<IMetricRepository, MetricRepository>();

        return services;
    }
}
