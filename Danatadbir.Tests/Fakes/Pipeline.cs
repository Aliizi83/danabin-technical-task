using Danatadbir.Application.AlertService;
using Danatadbir.Application.IngestionService;
using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Application.ReadingService;
using Danatadbir.Infrastructure.Services.Alerting;
using Danatadbir.Infrastructure.Services.Ingestion;
using Danatadbir.Infrastructure.Services.Readings;
using Danatadbir.Infrastructure.Services.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using IngestionServiceImpl = Danatadbir.Infrastructure.Services.Ingestion.IngestionService;

namespace Danatadbir.Tests.Fakes;

public class FakeLineSource : IReadingLineSource
{
    public IReadOnlyList<string> Lines { get; set; } = [];

    public bool Exists(string path) => path != "missing";

    public async IAsyncEnumerable<string> ReadLinesAsync(
        string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var line in Lines)
        {
            await Task.Yield();
            yield return line;
        }
    }
}

/// <summary>The real services wired to in-memory stores, so a test drives the whole pipeline.</summary>
public class Pipeline
{
    public const string HighTemperature = """{"threshold":90}""";
    public const string Sustained = """{"threshold":70.5,"durationSeconds":60}""";

    public InMemorySensorDataRepository Readings { get; } = new();
    public InMemoryRuleResultRepository RuleResults { get; } = new();
    public InMemoryAlertRepository Alerts { get; } = new();
    public FakeLineSource Source { get; } = new();
    public RuleCatalog Catalog { get; }
    public IIngestionService Ingestion { get; }
    public IReadingQueryService Queries { get; }
    public IAlertQueryService AlertQueries { get; }

    public Pipeline(params string[] rules)
    {
        Catalog = TestCatalog.FromRules(rules.Length > 0 ? rules : DefaultRules());

        var sensors = new InMemorySensorRepository("PUMP-01", "PUMP-02", "COMP-01", "FAN-03");
        var metrics = new InMemoryMetricRepository("temperature", "pressure", "vibration");
        var alerting = new AlertingService(Catalog, Alerts, NullLogger<AlertingService>.Instance);

        Ingestion = new IngestionServiceImpl(
            Source, Readings, sensors, metrics, RuleResults, Catalog,
            new RuleEvaluationService(Catalog), new EpisodeDetectionService(Catalog), alerting,
            Options.Create(new IngestionOptions { WriteBatchSize = 3, RejectionSampleLimit = 50 }),
            NullLogger<IngestionServiceImpl>.Instance);

        Queries = new ReadingQueryService(sensors, metrics, Readings, RuleResults);
        AlertQueries = new AlertQueryService(sensors, metrics, Alerts);
    }

    public static string[] DefaultRules() =>
    [
        TestCatalog.Rule("temp-high", "temperature", "GreaterThan", HighTemperature),
        TestCatalog.Rule("vibration-negative", "vibration", "LessThan", """{"threshold":0}"""),
        TestCatalog.Rule("pump01-sustained", "temperature", "SustainedAbove", Sustained, deviceId: "PUMP-01"),
        TestCatalog.Rule("off", "pressure", "GreaterThan", """{"threshold":1}""", enabled: false)
    ];

    public async Task<IngestionReportDto> RunAsync(params string[] lines) => await RunAsync((IEnumerable<string>)lines);

    public async Task<IngestionReportDto> RunAsync(IEnumerable<string> lines)
    {
        Source.Lines = lines.ToList();
        var result = await Ingestion.IngestAsync("feed");

        Assert.True(result.Success, result.Message);
        return result.Data!;
    }

    /// <summary>A feed line for the default sensor and metric, dated <paramref name="seconds"/> after the origin.</summary>
    public static string Line(double seconds, double value, long? seq = null, string sensor = "PUMP-01", string metric = "temperature") =>
        TestData.Line(sensor, metric, TestData.Iso(seconds), value, seq ?? (long)seconds + 1);
}
