using Danatadbir.Application.Common;
using Danatadbir.Application.IngestionService;
using Danatadbir.Infrastructure.Services;
using Danatadbir.Infrastructure.Services.Ingestion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class BindApplicationServices
{
    public static IServiceCollection BindServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReadingLineSource, JsonlFileReadingLineSource>();
        services.AddScoped<IIngestionService, Services.Ingestion.IngestionService>();

        return services;
    }
}
