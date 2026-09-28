using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Danatadbir.Api.Swagger.Configuration;

public class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = $"Danatadbir API {description.GroupName}",
                Version = description.ApiVersion.ToString(),
                Description = "Sensor ingestion, stateful rule engine and alerting."
                              + (description.IsDeprecated ? " This API version is deprecated." : string.Empty)
            });
        }
    }
}
