using Danatadbir.Application.Common;
using Danatadbir.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class BindApplicationServices
{
    public static IServiceCollection BindServices(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
