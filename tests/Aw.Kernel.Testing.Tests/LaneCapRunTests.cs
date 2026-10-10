namespace Aw.Kernel.Testing.Tests;

/// <summary>LaneCap.Run: the CLI contract over report files on disk (integration lane: it reads and writes temp files).</summary>
[Lane(TestLane.Integration)]
public sealed class LaneCapRunTests
{
    [Fact]
    public void Run_passes_a_clean_report_with_exit_0_and_prints_a_summary()
    {
        using var dir = new TempDir();
        var path = dir.Write("unit.json", LaneCapTests.Report(LaneCapTests.Test("Demo.Fast", 400)));
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
        var path = dir.Write("unit.json", LaneCapTests.Report(LaneCapTests.Test("Demo.Slow", 12_345), LaneCapTests.Test("Demo.NoLane", 1, labels: null)));
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
        _ = dir.Write("a.ctrf", LaneCapTests.Report(LaneCapTests.Test("Demo.Fast", 400)));
        _ = dir.Write("b.ctrf", LaneCapTests.Report(LaneCapTests.Test("Demo.Slow", 10_500)));
        _ = dir.Write("coverage.json", """{ "not": "ctrf" }""");
        _ = dir.Write("notes.txt", "not a report");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = LaneCap.Run([dir.Path], output, error);

        Assert.Equal(1, exit);
        var text = output.ToString();
        Assert.Contains("a.ctrf: 1 tests", text, StringComparison.Ordinal);
        Assert.Contains("LANE-TEST-001 Aw.Demo.Tests Demo.Slow", text, StringComparison.Ordinal);
        Assert.DoesNotContain("notes.txt", text, StringComparison.Ordinal);
        Assert.DoesNotContain("coverage.json", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_where_no_report_holds_a_test_is_exit_2()
    {
        // A crashed or misfiltered lane yields only empty reports; that must not pass as OK.
        using var dir = new TempDir();
        var path = dir.Write("empty.ctrf", LaneCapTests.Report());
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(2, LaneCap.Run([path], output, error));
        Assert.Contains("no tests in any report", error.ToString(), StringComparison.Ordinal);
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
}
