using System.Globalization;

namespace Aw.Kernel.Testing.Tests;

/// <summary>The per-test lane cap over xUnit's CTRF report (spec §4b.5). The reports are synthetic, in xUnit v3 4.0.1's CTRF shape.</summary>
[Lane(TestLane.Unit)]
public sealed class LaneCapTests
{
    private const string Suite = "s1";

    private static string Report(params string[] tests) => string.Create(
        CultureInfo.InvariantCulture,
        $$"""{ "reportFormat": "CTRF", "results": { "tool": { "name": "xUnit.net v3" }, "summary": { "tests": {{tests.Length}} }, "tests": [{{string.Join(',', tests)}}], "extra": { "suites": [ { "id": "{{Suite}}", "filePath": "/build/bin/Aw.Demo.Tests.dll" } ] } } }""");

    private static string Test(string name, int milliseconds, string? labels = """{ "Lane": "unit" }""") =>
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
    public void Run_passes_a_clean_report_with_exit_0_and_prints_a_summary()
    {
        using var dir = new TempDir();
        var path = dir.Write("unit.json", Report(Test("Demo.Fast", 400)));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = LaneCap.Run([path], output, error);

        Assert.Equal(0, exit);
        Assert.Equal($"lane-cap {path}: 1 tests, slowest Demo.Fast 0.400 s (unit), 0 violation(s)\nOK\n", output.ToString().ReplaceLineEndings("\n"));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void Run_fails_a_report_over_the_cap_with_exit_1_in_plain_text()
    {
        using var dir = new TempDir();
        var path = dir.Write("unit.json", Report(Test("Demo.Slow", 12_345), Test("Demo.NoLane", 1, labels: null)));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = LaneCap.Run([path], output, error);

        Assert.Equal(1, exit);
        var lines = output.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        Assert.StartsWith("LANE-TEST-001 Aw.Demo.Tests Demo.Slow: 12.345 s in lane unit, limit 10 s", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("LANE-TEST-002 Aw.Demo.Tests Demo.NoLane: lane traits []", lines[1], StringComparison.Ordinal);
        Assert.Equal("FAIL: a test breaks a lane rule (spec §4b.5)", lines[^1]);
        Assert.DoesNotContain('\u001b', output.ToString());
    }

    [Fact]
    public void Run_without_a_report_or_with_a_missing_file_is_exit_2()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(2, LaneCap.Run([], output, error));
        Assert.Equal(2, LaneCap.Run([Path.Combine(Path.GetTempPath(), "aw-lane-cap-missing", "none.json")], output, error));
        Assert.Contains("usage: lane-cap", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("none.json", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_on_a_directory_checks_every_ctrf_report_in_it()
    {
        // dotnet test --solution with --report-xunit-ctrf and no file name writes one '<user>_<host>_<time>.ctrf' per test
        // project; a fixed --report-xunit-ctrf-filename would make the projects overwrite each other's report.
        using var dir = new TempDir();
        _ = dir.Write("a.ctrf", Report(Test("Demo.Fast", 400)));
        _ = dir.Write("b.json", Report(Test("Demo.Slow", 10_500)));
        _ = dir.Write("notes.txt", "not a report");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = LaneCap.Run([dir.Path], output, error);

        Assert.Equal(1, exit);
        var text = output.ToString();
        Assert.Contains("a.ctrf: 1 tests", text, StringComparison.Ordinal);
        Assert.Contains("LANE-TEST-001 Aw.Demo.Tests Demo.Slow", text, StringComparison.Ordinal);
        Assert.DoesNotContain("notes.txt", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_on_a_directory_without_reports_is_exit_2()
    {
        using var dir = new TempDir();
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(2, LaneCap.Run([dir.Path], output, error));
        Assert.Contains("no CTRF reports", error.ToString(), StringComparison.Ordinal);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("aw-lane-cap-").FullName;

        public string Write(string name, string text)
        {
            var file = System.IO.Path.Combine(Path, name);
            File.WriteAllText(file, text);
            return file;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
