using Danatadbir.Application.Common.Result;
using Danatadbir.Application.ReadingService.Dtos;

namespace Danatadbir.Application.ReadingService;

public interface IReadingQueryService
{
    /// <summary>
    /// Per-bucket count, average, minimum and maximum over the <b>acceptable</b> readings of one
    /// sensor and metric in [from, to). Unacceptable readings are excluded.
    /// </summary>
    Task<BaseResult<AggregationResultDto>> AggregateAsync(
        AggregationQueryDto query,
        CancellationToken cancellationToken = default);

    Task<BaseResult<List<AcceptableReadingDto>>> GetAcceptableAsync(
        ReadingsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<BaseResult<List<UnacceptableReadingDto>>> GetUnacceptableAsync(
        ReadingsQueryDto query,
        CancellationToken cancellationToken = default);
}
