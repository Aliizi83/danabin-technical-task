using Danatadbir.Domain.Entities;

namespace Danatadbir.Domain.Repositories;

public interface ISensorDataRepository
{
    Task WriteAsync(IReadOnlyCollection<SensorData> readings, CancellationToken cancellationToken = default);

    Task<List<SensorData>> GetRangeAsync(
        string sensorExternalId,
        string metricKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
