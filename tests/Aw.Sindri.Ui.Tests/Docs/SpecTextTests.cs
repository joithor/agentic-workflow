using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Docs;

/// <summary>The UI spec's wording where the API contract settled it (U3' T14): the lines the screens depend on.</summary>
[Lane(TestLane.Unit)]
public sealed class SpecTextTests
{
    private static readonly string[] _lines = File.ReadAllLines(UiSource.Spec);

    [Fact]
    public void Spec_names_SND_API_011_for_read_your_writes()
    {
        var line = Line("read-your-writes timeouts");
        Assert.Contains("`SND-API-011`", line, StringComparison.Ordinal);
        Assert.DoesNotContain("`SND-API-004`", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Spec_says_X_picks_the_rejectLike_option()
    {
        var line = Line("| `X` / `Shift+X` |");
        Assert.Contains("the option whose `rejectLike` is true", line, StringComparison.Ordinal);
        Assert.Contains("on a decision", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Spec_says_recordedOnly_is_served_as_notice()
    {
        var line = Line("| `decision` | The question");
        Assert.Contains("the served `notice`", line, StringComparison.Ordinal);
        Assert.DoesNotContain("as \"recorded;", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Spec_says_the_failure_row_primary_is_RunService_pending_P6()
    {
        var line = Line("| `failure` | The service");
        Assert.Contains("`RunService`", line, StringComparison.Ordinal);
        Assert.Contains("`pending:P6`", line, StringComparison.Ordinal);
        Assert.Contains("`PauseService`", line, StringComparison.Ordinal);
        Assert.Contains("the served `cli`", line, StringComparison.Ordinal);
    }

    // §4.1 draws the strip; the fixture is the truth for its numbers (screens §4.9): the head's open count is the golden's, and
    // the overflow line is that count less the cap.
    [Fact]
    public void Screens_spec_strip_drawing_matches_the_fixture()
    {
        var lines = File.ReadAllLines(UiSource.ScreensSpec);
        var head = lines.First(l => l.StartsWith("┌ WORKFLOWS", StringComparison.Ordinal));
        var more = lines.First(l => l.StartsWith("│ … ", StringComparison.Ordinal) && l.Contains(" more", StringComparison.Ordinal));
        var open = FixtureInstances.Counts.Open;

        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"WORKFLOWS {open} open"), head, StringComparison.Ordinal);
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"… {open - WorkflowsStripViewModel.Cap} more"), more, StringComparison.Ordinal);
    }

    private static string Line(string start)
    {
        var line = _lines.FirstOrDefault(l => l.Contains(start, StringComparison.Ordinal));
        Assert.NotNull(line);
        return line;
    }
}
