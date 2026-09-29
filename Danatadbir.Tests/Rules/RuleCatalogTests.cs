using Danatadbir.Domain.Rules.Parameters;
using Danatadbir.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Danatadbir.Infrastructure.Services.Rules;

namespace Danatadbir.Tests.Rules;

public class RuleCatalogTests
{
    private const string Gt = """{"threshold":90}""";
    private const string Sustained = """{"threshold":80,"durationSeconds":30}""";

    private static string Rule(string id, string op = "GreaterThan", string? parameters = null, string metric = "temperature") =>
        TestCatalog.Rule(id, metric, op, parameters ?? (op == "SustainedAbove" ? Sustained : Gt));

    [Fact]
    public void Valid_rules_are_loaded_with_their_operator_and_typed_parameters()
    {
        var catalog = TestCatalog.FromRules(Rule("a"), Rule("b", "SustainedAbove"));

        Assert.Equal(2, catalog.Rules.Count);
        Assert.Empty(catalog.RejectedRules);
        Assert.Equal("GreaterThan", catalog.Rules[0].OperatorKey);
        Assert.IsType<ThresholdParameters>(catalog.Rules[0].Parameters);
        Assert.IsType<SustainedAboveParameters>(catalog.Rules[1].Parameters);
    }

    [Fact]
    public void The_example_rule_from_the_task_loads_unchanged()
    {
        var catalog = TestCatalog.FromJson("""
            {"rules":[{"id":"x","name":"Overheating Sustained","deviceId":"PUMP-01","metric":"temperature",
              "operator":"SustainedAbove","operatorParameters":{"threshold":80,"durationSeconds":30},"enabled":true}]}
            """);

        Assert.Single(catalog.Rules);
    }

    [Fact]
    public void An_unknown_operator_rejects_only_that_rule()
    {
        var catalog = TestCatalog.FromRules(Rule("good"), Rule("bad", "Teleport"), Rule("also-good"));

        Assert.Equal(["good", "also-good"], catalog.Rules.Select(r => r.Id));
        var rejected = Assert.Single(catalog.RejectedRules);
        Assert.Equal("bad", rejected.RuleId);
        Assert.Contains("Teleport", rejected.Reason);
    }

    [Theory]
    [InlineData("GreaterThan", """{}""")]
    [InlineData("GreaterThan", """{"min":1,"max":2}""")]
    [InlineData("GreaterThan", """{"threshold":"high"}""")]
    [InlineData("Between", """{"threshold":1}""")]
    [InlineData("Between", """{"min":9,"max":1}""")]
    [InlineData("Equal", """{"value":1,"tolerance":-1}""")]
    [InlineData("SustainedAbove", """{"threshold":80}""")]
    [InlineData("SustainedAbove", """{"threshold":80,"durationSeconds":0}""")]
    [InlineData("SustainedAbove", """{"threshold":80,"durationSeconds":30,"maxGapSeconds":-5}""")]
    public void Parameters_that_do_not_fit_the_operator_reject_the_rule(string op, string parameters)
    {
        var catalog = TestCatalog.FromRules(Rule("r", op, parameters));

        Assert.Empty(catalog.Rules);
        Assert.Single(catalog.RejectedRules);
    }

    [Fact]
    public void Missing_parameters_reject_the_rule()
    {
        var catalog = TestCatalog.FromRules("""{"id":"r","name":"r","metric":"temperature","operator":"GreaterThan"}""");

        Assert.Empty(catalog.Rules);
        Assert.Contains("operatorParameters", Assert.Single(catalog.RejectedRules).Reason);
    }

    [Theory]
    [InlineData("""{"name":"n","metric":"temperature","operator":"GreaterThan","operatorParameters":{"threshold":1}}""")]
    [InlineData("""{"id":"r","metric":"temperature","operator":"GreaterThan","operatorParameters":{"threshold":1}}""")]
    [InlineData("""{"id":"r","name":"n","operator":"GreaterThan","operatorParameters":{"threshold":1}}""")]
    [InlineData("""{"id":"r","name":"n","metric":"temperature","operatorParameters":{"threshold":1}}""")]
    public void A_rule_missing_a_required_field_is_rejected(string rule)
    {
        var catalog = TestCatalog.FromRules(rule);

        Assert.Empty(catalog.Rules);
        Assert.Single(catalog.RejectedRules);
    }

    [Fact]
    public void A_duplicate_rule_id_rejects_the_later_rule()
    {
        var catalog = TestCatalog.FromRules(Rule("same"), Rule("same", "LessThan"));

        Assert.Equal("GreaterThan", Assert.Single(catalog.Rules).OperatorKey);
        Assert.Contains("already used", Assert.Single(catalog.RejectedRules).Reason);
    }

    [Fact]
    public void A_missing_rules_file_loads_no_rules_and_does_not_throw()
    {
        var logger = new ListLogger<RuleCatalog>();
        var catalog = new RuleCatalog(
            TestCatalog.AllOperators(),
            Microsoft.Extensions.Options.Options.Create(new Danatadbir.Application.RuleService.RuleCatalogOptions { FilePath = "/no/such/rules.json" }),
            new TestHostEnvironment("/no/such"), logger);

        Assert.Empty(catalog.Rules);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void Invalid_json_loads_no_rules_and_does_not_throw()
    {
        var logger = new ListLogger<RuleCatalog>();

        var catalog = TestCatalog.FromJson("{ this is not json", logger);

        Assert.Empty(catalog.Rules);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void A_rejected_rule_is_logged_as_an_error()
    {
        var logger = new ListLogger<RuleCatalog>();

        TestCatalog.FromJson($$"""{"rules":[{{Rule("bad", "Teleport")}}]}""", logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("bad"));
    }

    [Fact]
    public void A_disabled_rule_is_loaded_but_never_returned_for_a_reading()
    {
        var catalog = TestCatalog.FromRules(TestCatalog.Rule("off", "temperature", "GreaterThan", Gt, enabled: false));

        Assert.Single(catalog.Rules);
        Assert.Empty(catalog.RulesFor("PUMP-01", "temperature"));
    }

    [Fact]
    public void Rules_for_a_series_include_fleet_wide_and_device_specific_rules_only()
    {
        var catalog = TestCatalog.FromRules(
            TestCatalog.Rule("fleet", "temperature", "GreaterThan", Gt),
            TestCatalog.Rule("mine", "temperature", "GreaterThan", Gt, deviceId: "PUMP-01"),
            TestCatalog.Rule("theirs", "temperature", "GreaterThan", Gt, deviceId: "PUMP-02"),
            TestCatalog.Rule("other-metric", "pressure", "GreaterThan", Gt));

        Assert.Equal(["fleet", "mine"], catalog.RulesFor("PUMP-01", "temperature").Select(r => r.Id));
    }

    [Fact]
    public void The_cooldown_defaults_to_five_minutes_and_can_be_set_per_rule()
    {
        var catalog = TestCatalog.FromRules(
            TestCatalog.Rule("default", "temperature", "SustainedAbove", Sustained),
            TestCatalog.Rule("custom", "temperature", "SustainedAbove", Sustained, cooldownSeconds: 90));

        Assert.Equal(TimeSpan.FromMinutes(5), catalog.Rules[0].AlertCooldown);
        Assert.Equal(TimeSpan.FromSeconds(90), catalog.Rules[1].AlertCooldown);
    }

    [Fact]
    public void A_negative_cooldown_rejects_the_rule()
    {
        var catalog = TestCatalog.FromRules(TestCatalog.Rule("r", "temperature", "SustainedAbove", Sustained, cooldownSeconds: -1));

        Assert.Empty(catalog.Rules);
    }

    [Fact]
    public void A_cooldown_on_a_stateless_rule_is_ignored_with_a_warning()
    {
        var logger = new ListLogger<RuleCatalog>();

        var catalog = TestCatalog.FromJson(
            $$"""{"rules":[{{TestCatalog.Rule("r", "temperature", "GreaterThan", Gt, cooldownSeconds: 60)}}]}""", logger);

        Assert.Single(catalog.Rules);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("ignored"));
    }

    [Fact]
    public void The_supplied_rules_file_loads_completely()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Data/rules.json"));
        var catalog = TestCatalog.FromJson(File.ReadAllText(path));

        Assert.Empty(catalog.RejectedRules);
        Assert.Equal(10, catalog.Rules.Count);
    }
}
