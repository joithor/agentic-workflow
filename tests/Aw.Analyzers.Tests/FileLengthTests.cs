using System.Text;
using Aw.Kernel.Testing;

namespace Aw.Analyzers.Tests;

[Lane(TestLane.Unit)]
public sealed class FileLengthTests
{
    private static string FileOf(int lines)
    {
        var text = new StringBuilder("namespace Aw.Probe;\n");
        for (var i = 2; i < lines; i++)
        {
            _ = text.Append("// line\n");
        }

        return text.Append("internal static class C { }\n").ToString();
    }

    [Fact]
    public async Task A_300_line_file_passes() =>
        Assert.Empty(await AnalyzerHarness.Run(new FileLengthAnalyzer(), "Aw.Probe", FileOf(300)));

    [Fact]
    public async Task A_301_line_file_fails_on_its_first_line_over_the_limit() =>
        Assert.Equal(["AW1005@301"], await AnalyzerHarness.Run(new FileLengthAnalyzer(), "Aw.Probe", FileOf(301)));

    [Fact]
    public async Task Message_states_lines_and_limit()
    {
        var diagnostics = await AnalyzerHarness.Diagnostics(new FileLengthAnalyzer(), "Aw.Probe", ("Big.cs", FileOf(320)));

        var message = Assert.Single(diagnostics).GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains("has 320 lines; the limit is 300", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generated_files_are_skipped() =>
        Assert.Empty(await AnalyzerHarness.Run(new FileLengthAnalyzer(), "Aw.Probe", ("Big.g.cs", FileOf(400))));
}
