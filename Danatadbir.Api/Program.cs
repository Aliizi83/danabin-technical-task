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

app.MapGet("/", () => Results.Redirect("/swagger", permanent: false)).ExcludeFromDescription();

app.MapControllers();
app.MapHealthChecks("/health");

// Described after every endpoint is mapped, so minimal APIs are covered too.
var apiVersions = app.DescribeApiVersions();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    foreach (var description in apiVersions)
    {
        options.SwaggerEndpoint(
            $"/swagger/{description.GroupName}/swagger.json",
            $"Danatadbir API {description.GroupName}");
    }
});

app.Run();
