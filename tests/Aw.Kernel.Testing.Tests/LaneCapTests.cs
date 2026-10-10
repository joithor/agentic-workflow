using System.Globalization;

namespace Aw.Kernel.Testing.Tests;

/// <summary>The per-test lane cap over xUnit's CTRF report (spec §4b.5). The reports are synthetic, in xUnit v3 4.0.1's CTRF shape.</summary>
[Lane(TestLane.Unit)]
public sealed class LaneCapTests
{
    internal const string Suite = "s1";

    internal static string Report(params string[] tests) => string.Create(
        CultureInfo.InvariantCulture,
        $$"""{ "reportFormat": "CTRF", "results": { "tool": { "name": "xUnit.net v3" }, "summary": { "tests": {{tests.Length}} }, "tests": [{{string.Join(',', tests)}}], "extra": { "suites": [ { "id": "{{Suite}}", "filePath": "/build/bin/Aw.Demo.Tests.dll" } ] } } }""");

    internal static string Test(string name, int milliseconds, string? labels = """{ "Lane": "unit" }""") =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "name": "{{name}}", "status": "passed", "duration": {{milliseconds}}, "suite": ["{{Suite}}", "c", "T", "M"]{{(labels is null ? string.Empty : ", \"labels\": " + labels)}} }""");

    [Fact]
    public void Tests_within_their_lane_caps_pass()
    {
        var report = LaneCap.Check(Report(
            Test("Demo.Fast", 400),
            Test("Demo.AtTheUnitCap", 10_000),
            Test("Demo.Db", 59_000, """{ "Lane": "integration" }"""),
            Test("Demo.Browser", 900_000, """{ "Lane": "heavy" }""")));

        Assert.True(report.Passed);
        Assert.Equal(4, report.Tests);
        Assert.Equal(("Demo.Browser", 900.0, "heavy"), report.Slowest);
    }

    [Fact]
    public void A_unit_test_over_10_s_breaks_the_cap_with_the_rule_value_limit_and_fix()
    {
        var report = LaneCap.Check(Report(Test("Demo.Slow", 10_001)));

        var violation = Assert.Single(report.Violations);
        Assert.Equal(("LANE-TEST-001", "Aw.Demo.Tests", "Demo.Slow"), (violation.RuleId, violation.Package, violation.Test));
        Assert.Equal(
            "LANE-TEST-001 Aw.Demo.Tests Demo.Slow: 10.001 s in lane unit, limit 10 s per test. Fix: make the test faster, or if it does real I/O move it to a slower lane ([Lane(TestLane.Integration)] or Heavy).",
            violation.Message);
    }

    [Fact]
    public void An_integration_test_over_60_s_breaks_the_cap_and_is_pointed_at_the_heavy_lane()
    {
        var violation = Assert.Single(LaneCap.Check(Report(Test("Demo.Db", 60_500, """{ "Lane": "integration" }"""))).Violations);

        Assert.Equal(
            "LANE-TEST-001 Aw.Demo.Tests Demo.Db: 60.500 s in lane integration, limit 60 s per test. Fix: make the test faster, or if it needs real containers, browsers or index builds move it to the heavy lane ([Lane(TestLane.Heavy)], nightly).",
            violation.Message);
    }

    [Theory]
    [InlineData(null, "[]")]
    [InlineData("""{ "Lane": ["unit", "integration"] }""", "[unit, integration]")]
    [InlineData("""{ "Lane": "fast" }""", "[fast]")]
    [InlineData("""{ "Category": "unit" }""", "[]")]
    public void A_test_without_exactly_one_known_lane_breaks_the_lane_rule(string? labels, string seen)
    {
        var violation = Assert.Single(LaneCap.Check(Report(Test("Demo.Odd", 5, labels))).Violations);

        Assert.Equal("LANE-TEST-002", violation.RuleId);
        Assert.Contains($"lane traits {seen}", violation.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "results": { "summary": {} } }""")]
    [InlineData("""{ "results": { "tests": [ { "name": "Demo.NoDuration" } ] } }""")]
    public void Input_that_is_not_a_CTRF_report_is_an_input_error(string text) =>
        Assert.Throws<InvalidDataException>(() => LaneCap.Check(text));

    [Fact]
    public void A_report_whose_suites_are_not_objects_is_an_input_error() =>
        Assert.Throws<InvalidDataException>(() => LaneCap.Check("""{ "results": { "tests": [], "extra": { "suites": [1] } } }"""));
}
