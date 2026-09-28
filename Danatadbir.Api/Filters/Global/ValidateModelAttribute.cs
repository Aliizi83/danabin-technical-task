using Danatadbir.Application.Common.Result;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Danatadbir.Api.Filters.Global;

public class ValidateModelAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
            return;

        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => $"{entry.Key}: {error.ErrorMessage}"));

        context.Result = new BadRequestObjectResult(new BaseResult
        {
            Success = false,
            StatusCode = StatusCodes.Status400BadRequest,
            Message = string.Join(" | ", errors)
        });
    }
}
