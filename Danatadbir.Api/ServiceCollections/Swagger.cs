using System.Reflection;
using Danatadbir.Api.Swagger.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Api.ServiceCollections;

public static class Swagger
{
    public static IServiceCollection RegisterSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        services.ConfigureOptions<ConfigureSwaggerOptions>();

        return services;
    }
}
