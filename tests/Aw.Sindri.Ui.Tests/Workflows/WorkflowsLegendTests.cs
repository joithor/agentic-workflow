using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The legend at the foot of the Workflows page (screens §4.2): each mark with its word, and the same line in words alone for screen readers.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsLegendTests
{
    private readonly FakeSindriApi _server = new();

    [Fact]
    public void The_legend_names_every_mark_the_rows_draw_and_the_keys_in_words()
    {
        var list = new WorkflowsViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        var marks = new[] { StageCellViewModel.Mark(Ui.Api.Workflow.StateGlyph.Done), StageCellViewModel.Mark(Ui.Api.Workflow.StateGlyph.Active), StageCellViewModel.Mark(Ui.Api.Workflow.StateGlyph.Pending), Labels.LoopMark };
        var words = new[] { Labels.StageDone, Labels.StageActive, Labels.StagePending, Labels.LegendLoop, Labels.LegendEscalated, Labels.LegendTakenOver };

        Assert.All(marks, m => Assert.Contains(m, list.LegendText, StringComparison.Ordinal));
        Assert.All(words, w => Assert.Contains(w, list.LegendText, StringComparison.Ordinal));
        Assert.All(words, w => Assert.Contains(w, list.LegendName, StringComparison.Ordinal));
        Assert.All(marks, m => Assert.DoesNotContain(m, list.LegendName, StringComparison.Ordinal));
        Assert.All((string[])["Enter", "A", "B", "X"], k => Assert.Contains(k, list.LegendText, StringComparison.Ordinal));
    }

    // A does nothing on a parked desk-class row (its answer is a command to copy), so the legend says when it applies (L6).
    [Fact]
    public void The_legend_says_when_A_applies()
    {
        var list = new WorkflowsViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));

        Assert.Contains("A answers a decision or finishes a task that is waiting for you", list.LegendText, StringComparison.Ordinal);
        Assert.Contains("A answers a decision or finishes a task that is waiting for you", list.LegendName, StringComparison.Ordinal);
    }

    // B does not hand a run back in the app yet (HandBackArrives), so the legend must not promise it does.
    [Fact]
    public void The_legend_says_what_B_does_as_the_key_does()
    {
        var list = new WorkflowsViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));

        Assert.DoesNotContain("B hands the run back", list.LegendText, StringComparison.Ordinal);
        Assert.DoesNotContain("B hands the run back", list.LegendName, StringComparison.Ordinal);
        Assert.Contains("B shows how to hand a run back", list.LegendText, StringComparison.Ordinal);
        Assert.Contains("B shows how to hand a run back", list.LegendName, StringComparison.Ordinal);
    }

    [Fact]
    public void The_key_and_the_notes_use_plain_words_and_no_phase_names()
    {
        Assert.Equal(("paused: needs a decision", "you took over", "repeats"), (Labels.LegendEscalated, Labels.LegendTakenOver, Labels.LegendLoop));
        Assert.Equal("Handing a run back isn't available in the app yet. Copy its command from the run and run it at a terminal.", Labels.HandBackArrives);
        Assert.Equal("This Sindri answered in a form this app can't read yet. Updating either one usually fixes it.", Labels.StripUnreadable);
        Assert.All([Labels.HandBackArrives, Labels.StripUnreadable, Labels.ArrivesWorkflowStrip], text => Assert.DoesNotMatch(@"\b(U\d+b?|P\d+)\b", text));
    }
}
