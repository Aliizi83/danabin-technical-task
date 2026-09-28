using System.Net;
using Asp.Versioning;
using Danatadbir.Api.Controllers.Common;
using Danatadbir.Application.Common.Result;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Danatadbir.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
public class HealthController(HealthCheckService healthCheckService) : BaseController
{
    /// <summary>Reports connectivity to PostgreSQL and InfluxDB.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResult<HealthReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResult<HealthReportDto>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        var dto = new HealthReportDto(
            report.Status.ToString(),
            report.TotalDuration.TotalMilliseconds,
            report.Entries.Select(entry => new HealthEntryDto(
                entry.Key,
                entry.Value.Status.ToString(),
                entry.Value.Description,
                entry.Value.Duration.TotalMilliseconds)).ToList());

        var result = report.Status == HealthStatus.Healthy
            ? BaseResult<HealthReportDto>.Ok(dto)
            : new BaseResult<HealthReportDto>
            {
                Success = false,
                Data = dto,
                Message = "One or more dependencies are unavailable.",
                StatusCode = (int)HttpStatusCode.ServiceUnavailable
            };

        return HandleResult(result);
    }
}

public record HealthReportDto(string Status, double DurationMs, IReadOnlyList<HealthEntryDto> Dependencies);

public record HealthEntryDto(string Name, string Status, string? Description, double DurationMs);
