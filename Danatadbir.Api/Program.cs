using Asp.Versioning.ApiExplorer;
using Danatadbir.Api.Middlewares;
using Danatadbir.Api.ServiceCollections;
using Danatadbir.Infrastructure.ServiceCollections;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddCors(options => options.AddPolicy("dev", policy =>
    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCors("dev");
}
else
{
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerEndpoint(
            $"/swagger/{description.GroupName}/swagger.json",
            $"Danatadbir API {description.GroupName}");
    }
});

app.MapGet("/", () => Results.Redirect("/swagger", permanent: false)).ExcludeFromDescription();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
