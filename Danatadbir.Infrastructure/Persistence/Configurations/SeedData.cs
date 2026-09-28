using Danatadbir.Domain.Entities;

namespace Danatadbir.Infrastructure.Persistence.Configurations;
internal static class SeedData
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Sensor[] Sensors =
    [
        Sensor(1, "PUMP-01", "Pump 01"),
        Sensor(2, "PUMP-02", "Pump 02"),
        Sensor(3, "COMP-01", "Compressor 01"),
        Sensor(4, "FAN-03", "Fan 03")
    ];

    public static readonly Metric[] Metrics =
    [
        Metric(1, "temperature", "Temperature", "°C"),
        Metric(2, "pressure", "Pressure", "bar"),
        Metric(3, "vibration", "Vibration", "mm/s")
    ];

    private static Sensor Sensor(int id, string externalId, string title) => new()
    {
        Id = id,
        ExternalId = externalId,
        Title = title,
        CreatedAt = SeededAt,
        UpdatedAt = SeededAt,
        IsActive = true
    };

    private static Metric Metric(int id, string key, string title, string unit) => new()
    {
        Id = id,
        Key = key,
        Title = title,
        Unit = unit,
        CreatedAt = SeededAt,
        UpdatedAt = SeededAt,
        IsActive = true
    };
}
