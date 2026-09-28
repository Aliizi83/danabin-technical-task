using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class ServiceCollector
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAppDbContext(configuration);
        services.AddApplicationInfluxDb(configuration);

        services.BindDomainRepositories();
        services.BindServices();

        services.AddInfrastructureHealthChecks();

        return services;
    }
}
