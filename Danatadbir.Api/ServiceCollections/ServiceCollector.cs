using Danatadbir.Api.Filters.Global;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Api.ServiceCollections;

public static class ServiceCollector
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterApiVersioning();
        services.RegisterSwagger();

        services.AddHttpContextAccessor();
        services.AddControllers(options => options.Filters.Add<ValidateModelAttribute>());

        return services;
    }
}
