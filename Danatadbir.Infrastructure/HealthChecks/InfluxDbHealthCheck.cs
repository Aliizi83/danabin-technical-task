using Danatadbir.Infrastructure.Options;
using InfluxDB.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Danatadbir.Infrastructure.HealthChecks;

public class InfluxDbHealthCheck(IInfluxDBClient client, IOptions<InfluxDbOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reachable = await client.PingAsync();

            return reachable
                ? HealthCheckResult.Healthy($"InfluxDB is reachable (bucket: {options.Value.Bucket})")
                : HealthCheckResult.Unhealthy("InfluxDB did not answer the ping");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("InfluxDB connection failed", exception);
        }
    }
}
