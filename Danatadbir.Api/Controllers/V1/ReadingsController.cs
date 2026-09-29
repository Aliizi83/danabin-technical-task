using Asp.Versioning;
using Danatadbir.Api.Controllers.Common;
using Danatadbir.Application.Common.Result;
using Danatadbir.Application.ReadingService;
using Danatadbir.Application.ReadingService.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Danatadbir.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/readings")]
public class ReadingsController(IReadingQueryService readingQueryService) : BaseController
{
    /// <summary>
    /// Time-bucketed aggregates of the acceptable readings of one sensor and metric.
    /// </summary>
    /// <remarks>
    /// Buckets are equally sized and start at <c>from</c>; the range is half-open, [from, to).
    /// Only acceptable readings are counted: readings that violated a rule are excluded.
    /// Buckets that contain no readings are omitted. A value without an offset is read as UTC.
    /// </remarks>
    /// <param name="deviceId">Sensor id, e.g. PUMP-01 (case-insensitive).</param>
    /// <param name="metric">Metric key, e.g. temperature (case-insensitive).</param>
    /// <param name="from">Start of the range, inclusive (ISO-8601).</param>
    /// <param name="to">End of the range, exclusive (ISO-8601).</param>
    /// <param name="bucketSeconds">Bucket size in seconds.</param>
    [HttpGet("aggregates")]
    [ProducesResponseType(typeof(BaseResult<AggregationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResult<AggregationResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BaseResult<AggregationResultDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Aggregate(
        [FromQuery] string? deviceId,
        [FromQuery] string? metric,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int bucketSeconds,
        CancellationToken cancellationToken) =>
        HandleResult(await readingQueryService.AggregateAsync(
            new AggregationQueryDto(deviceId, metric, from, to, bucketSeconds), cancellationToken));

    /// <summary>Readings that broke no rule, ordered by time.</summary>
    [HttpGet("acceptable")]
    [ProducesResponseType(typeof(BaseResult<List<AcceptableReadingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Acceptable(
        [FromQuery] string? deviceId,
        [FromQuery] string? metric,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        HandleResult(await readingQueryService.GetAcceptableAsync(
            new ReadingsQueryDto(deviceId, metric, from, to, page, pageSize), cancellationToken));

    /// <summary>Readings that violated at least one rule, with the rules violated and why.</summary>
    [HttpGet("unacceptable")]
    [ProducesResponseType(typeof(BaseResult<List<UnacceptableReadingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unacceptable(
        [FromQuery] string? deviceId,
        [FromQuery] string? metric,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        HandleResult(await readingQueryService.GetUnacceptableAsync(
            new ReadingsQueryDto(deviceId, metric, from, to, page, pageSize), cancellationToken));
}
