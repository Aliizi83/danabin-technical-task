using Danatadbir.Application.AlertService.Dtos;
using Danatadbir.Application.Common.Result;

namespace Danatadbir.Application.AlertService;

public interface IAlertQueryService
{
    Task<BaseResult<List<AlertDto>>> GetAsync(
        string? deviceId,
        string? metric,
        string? ruleId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
