using Danatadbir.Application.RuleService.Dtos;
using Danatadbir.Domain.Entities;

namespace Danatadbir.Application.RuleService;

public interface IEpisodeDetectionService
{
    IReadOnlyList<RuleEpisodeDto> Detect(
        string sensorExternalId,
        string metricKey,
        IReadOnlyList<SensorData> series);
}
