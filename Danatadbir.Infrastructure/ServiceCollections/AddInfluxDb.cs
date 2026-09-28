using Danatadbir.Infrastructure.Options;
using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Danatadbir.Infrastructure.ServiceCollections;

public static class AddInfluxDb
{
    public static IServiceCollection AddApplicationInfluxDb(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(InfluxDbOptions.SectionName);
        services.Configure<InfluxDbOptions>(section);

        var options = section.Get<InfluxDbOptions>()
                      ?? throw new InvalidOperationException($"Configuration section '{InfluxDbOptions.SectionName}' is missing.");

        services.AddSingleton<IInfluxDBClient>(_ => new InfluxDBClient(options.Url, options.Token));

        return services;
    }
}
