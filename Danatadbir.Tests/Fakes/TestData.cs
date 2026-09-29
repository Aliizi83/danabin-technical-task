using Danatadbir.Domain.Entities;

namespace Danatadbir.Tests.Fakes;

public static class TestData
{
    public static readonly DateTime Origin = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime At(double seconds) => Origin.AddSeconds(seconds);

    public static SensorData Reading(
        double seconds, double value, long? seq = null, string sensor = "PUMP-01", string metric = "temperature") =>
        new(sensor, metric, At(seconds), value, seq ?? (long)seconds + 1);

    /// <summary>Readings every <paramref name="step"/> seconds from <paramref name="from"/> to <paramref name="to"/> inclusive.</summary>
    public static List<SensorData> Run(double from, double to, double value, double step = 10, string sensor = "PUMP-01") =>
        Enumerable.Range(0, (int)((to - from) / step) + 1)
            .Select(i => Reading(from + i * step, value, sensor: sensor))
            .ToList();

    public static string Line(string sensor, string metric, string ts, double value, long seq) =>
        $$"""{"deviceId":"{{sensor}}","metric":"{{metric}}","ts":"{{ts}}","value":{{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"seq":{{seq}}}""";

    public static string Iso(double seconds) => At(seconds).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
}
