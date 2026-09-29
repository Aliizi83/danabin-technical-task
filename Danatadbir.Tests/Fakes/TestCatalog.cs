using Danatadbir.Application.RuleService;
using Danatadbir.Domain.Rules;
using Danatadbir.Domain.Rules.Operators;
using Danatadbir.Infrastructure.Services.Rules;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Danatadbir.Tests.Fakes;

public static class TestCatalog
{
    public static IRuleOperator[] AllOperators() =>
    [
        new GreaterThanOperator(), new GreaterThanOrEqualOperator(), new LessThanOperator(),
        new LessThanOrEqualOperator(), new EqualOperator(), new BetweenOperator(), new SustainedAboveOperator()
    ];

    /// <summary>Builds the real catalog from rules.json content, exactly as at startup.</summary>
    public static RuleCatalog FromJson(string json, ILogger<RuleCatalog>? logger = null)
    {
        var directory = Directory.CreateTempSubdirectory("danatadbir-tests");
        var path = Path.Combine(directory.FullName, "rules.json");
        File.WriteAllText(path, json);

        return new RuleCatalog(
            AllOperators(),
            Options.Create(new RuleCatalogOptions { FilePath = path }),
            new TestHostEnvironment(directory.FullName),
            logger ?? NullLogger<RuleCatalog>.Instance);
    }

    public static RuleCatalog FromRules(params string[] ruleJson) =>
        FromJson($$"""{"rules":[{{string.Join(",", ruleJson)}}]}""");

    public static string Rule(
        string id, string metric, string op, string parameters,
        string? deviceId = null, bool enabled = true, int? cooldownSeconds = null) =>
        $$"""
          {"id":"{{id}}","name":"{{id}} rule","metric":"{{metric}}",
           "deviceId":{{(deviceId is null ? "null" : $"\"{deviceId}\"")}},
           "operator":"{{op}}","enabled":{{enabled.ToString().ToLowerInvariant()}},
           "operatorParameters":{{parameters}}
           {{(cooldownSeconds is null ? "" : $",\"alertCooldownSeconds\":{cooldownSeconds}")}}}
          """;
}

public class TestHostEnvironment(string contentRoot) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "Danatadbir.Tests";
    public string ContentRootPath { get; set; } = contentRoot;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
}
