using Asp.Versioning;
using Danatadbir.Api.Controllers.Common;
using Danatadbir.Application.Common.Result;
using Danatadbir.Application.IngestionService;
using Danatadbir.Application.IngestionService.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Danatadbir.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ingestion")]
public class IngestionController(IIngestionService ingestionService) : BaseController
{
    [HttpPost("run")]
    [ProducesResponseType(typeof(BaseResult<IngestionReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResult<IngestionReportDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Run([FromQuery] string? path, CancellationToken cancellationToken)
    {
        var result = await ingestionService.IngestAsync(path, cancellationToken);
        return HandleResult(result);
    }
}
