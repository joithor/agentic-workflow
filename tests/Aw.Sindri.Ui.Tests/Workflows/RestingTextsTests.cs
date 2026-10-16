using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>What the hidden live regions and the Copy status button say at rest (review run 2, a11y-r2-4 and a11y-r2-8): a button keeps its own words, and a region never reads as a stray heading or control.</summary>
[Lane(TestLane.Unit)]
public sealed class RestingTextsTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    [Fact]
    public async Task The_Copy_status_button_rests_on_its_own_words_and_returns_to_them()
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock);
        Assert.Equal(Labels.CopyStatus, expanded.StatusCopy.Label);

        await expanded.CopyStatus(TestContext.Current.CancellationToken);
        Assert.Equal(Labels.Copied, expanded.StatusCopy.Label);
        _clock.Advance(ProtectedActionViewModel.CopiedFor);

        Assert.Equal(Labels.CopyStatus, expanded.StatusCopy.Label);
    }

    [Fact]
    public void The_live_regions_rest_on_notes_words_that_repeat_no_heading_or_control()
    {
        var detail = FixtureInstances.Detail("project-build");
        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock);
        var list = new WorkflowsViewModel(new FakeSindriApi(), new FakeClipboard(), _clock);

        Assert.Equal(Labels.PendingNotesResting, expanded.PendingVoice.Announcement);
        Assert.Equal(Labels.KeyNotesResting, list.KeyNoteVoice.Announcement);
        Assert.DoesNotContain((string[])[expanded.PendingVoice.Announcement, list.KeyNoteVoice.Announcement], rest => string.Equals(rest, Labels.WorkflowsHeading, StringComparison.Ordinal) || string.Equals(rest, Labels.Cancel, StringComparison.Ordinal));
        Assert.EndsWith("notes", expanded.PendingVoice.Announcement, StringComparison.Ordinal);
    }
}
