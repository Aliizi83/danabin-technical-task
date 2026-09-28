using Danatadbir.Application.Common.Result;
using Danatadbir.Application.IngestionService.Dtos;

namespace Danatadbir.Application.IngestionService;

public interface IIngestionService
{
    Task<BaseResult<IngestionReportDto>> IngestAsync(string? path, CancellationToken cancellationToken = default);
}
