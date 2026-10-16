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
}
