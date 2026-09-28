using System.Net;
using Danatadbir.Application.Common.Result;
using Microsoft.AspNetCore.Mvc;

namespace Danatadbir.Api.Controllers.Common;

[ApiController]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    protected IActionResult HandleResult(BaseResult result) => ToActionResult(result.StatusCode, result);

    protected IActionResult HandleResult<TData>(BaseResult<TData> result) => ToActionResult(result.StatusCode, result);

    private IActionResult ToActionResult(int statusCode, object payload) => statusCode switch
    {
        (int)HttpStatusCode.OK => Ok(payload),
        (int)HttpStatusCode.Created => StatusCode(StatusCodes.Status201Created, payload),
        (int)HttpStatusCode.Accepted => Accepted(payload),
        (int)HttpStatusCode.NoContent => NoContent(),

        (int)HttpStatusCode.BadRequest => BadRequest(payload),
        (int)HttpStatusCode.Unauthorized => Unauthorized(payload),
        (int)HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, payload),
        (int)HttpStatusCode.NotFound => NotFound(payload),
        (int)HttpStatusCode.Conflict => Conflict(payload),
        (int)HttpStatusCode.UnprocessableEntity => UnprocessableEntity(payload),
        (int)HttpStatusCode.TooManyRequests => StatusCode(StatusCodes.Status429TooManyRequests, payload),

        _ => StatusCode(StatusCodes.Status500InternalServerError, payload)
    };
}
