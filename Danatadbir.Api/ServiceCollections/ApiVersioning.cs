using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Api.ServiceCollections;

public static class ApiVersioning
{
    public static IServiceCollection RegisterApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddEndpointsApiExplorer();

        return services;
    }
}
