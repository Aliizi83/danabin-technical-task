using Asp.Versioning;
using Danatadbir.Api.Controllers.Common;
using Danatadbir.Application.AlertService;
using Danatadbir.Application.AlertService.Dtos;
using Danatadbir.Application.Common.Result;
using Microsoft.AspNetCore.Mvc;

namespace Danatadbir.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/alerts")]
public class AlertsController(IAlertQueryService alertQueryService) : BaseController
{
    /// <summary>Alerts raised by sustained rules, ordered by start time. Every filter is optional.</summary>
    /// <param name="from">Earliest alert start, inclusive.</param>
    /// <param name="to">Latest alert start, exclusive.</param>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResult<List<AlertDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] string? deviceId,
        [FromQuery] string? metric,
        [FromQuery] string? ruleId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        HandleResult(await alertQueryService.GetAsync(
            deviceId, metric, ruleId, from, to, page, pageSize, cancellationToken));
}
