using Danatadbir.Domain.Repositories;
using Danatadbir.Infrastructure.Repositories;
using Danatadbir.Infrastructure.TimeSeries;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class BindRepositories
{
    public static IServiceCollection BindDomainRepositories(this IServiceCollection services)
    {
        services.AddScoped<ISensorRepository, SensorRepository>();
        services.AddScoped<IMetricRepository, MetricRepository>();
        services.AddScoped<ISensorDataRepository, InfluxSensorDataRepository>();

        return services;
    }
}
