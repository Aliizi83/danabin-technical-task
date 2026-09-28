using System.Net;
using System.Text.Json;
using Danatadbir.Application.Common.Result;

namespace Danatadbir.Api.Middlewares;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var result = BaseResult.Failure(HttpStatusCode.InternalServerError, "An unexpected error occurred.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(result, SerializerOptions));
        }
    }
}
