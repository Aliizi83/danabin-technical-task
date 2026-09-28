using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class BindRepositories
{
    public static IServiceCollection BindDomainRepositories(this IServiceCollection services)
    {
        // Domain repositories are registered here, e.g.
        // services.AddScoped<IReadingRepository, ReadingRepository>();

        return services;
    }
}
