using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>What a screen reader gets from the expanded state's live regions (review run 1, V9): the flag goes on before the text changes, a text is said once, and the text is never empty.</summary>
[Lane(TestLane.Unit)]
public sealed class ExpandedStateVoiceTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ExpandedStateViewModel Expanded(string scenario)
    {
        var detail = FixtureInstances.Detail(scenario);
        return new ExpandedStateViewModel(detail.Current, detail.Address, _clipboard, _clock);
    }

    // What a screen reader gets from the live region: the flag goes on before the text changes, the text is "Copied" once, and never "".
    private static List<(bool Live, string Text)> Heard(CopyAction copy) => Listen(copy, () => (copy.IsAnnouncing, copy.Announcement));

    private static List<(bool Live, string Text)> Heard(LiveAnnouncer voice) => Listen(voice, () => (voice.IsAnnouncing, voice.Announcement));

    private static List<(bool Live, string Text)> Listen(System.ComponentModel.INotifyPropertyChanged source, Func<(bool Live, string Text)> read)
    {
        var heard = new List<(bool Live, string Text)>();
        source.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, "Announcement", StringComparison.Ordinal) || string.Equals(e.PropertyName, "IsAnnouncing", StringComparison.Ordinal))
            {
                heard.Add(read());
            }
        };
        return heard;
    }

    [Fact]
    public async Task Copy_status_sets_the_live_region_once_and_rests_after_1_5_seconds()
    {
        var expanded = Expanded("escalated-gate");
        var heard = Heard(expanded.StatusCopy);

        await expanded.CopyStatus(Token);

        Assert.Equal(1, heard.Count(h => h.Live && string.Equals(h.Text, Labels.Copied, StringComparison.Ordinal)));
        Assert.DoesNotContain(heard, h => h.Text.Length == 0);
        Assert.Equal(Labels.Copied, expanded.StatusCopy.Label);
        _clock.Advance(ProtectedActionViewModel.CopiedFor);
        Assert.Equal((Labels.CopyStatus, false), (expanded.StatusCopy.Label, expanded.StatusCopy.IsAnnouncing));
        Assert.DoesNotContain(heard, h => h.Text.Length == 0);
    }

    [Fact]
    public async Task A_commands_Copy_sets_the_live_region_once()
    {
        var expanded = Expanded("dev-desk-round-trip");
        var command = expanded.Commands[0];
        var heard = Heard(command.CliCopy);

        await command.Copy(Token);

        Assert.Equal(1, heard.Count(h => h.Live && string.Equals(h.Text, Labels.Copied, StringComparison.Ordinal)));
        Assert.DoesNotContain(heard, h => h.Text.Length == 0);
        Assert.Equal(Labels.Copied, command.CliCopy.Label);
    }

    [Fact]
    public void Confirming_the_cancel_says_the_pending_note_once_and_asking_again_quiets_it()
    {
        var expanded = Expanded("project-build");
        var heard = Heard(expanded.PendingVoice);
        expanded.AskCancel();

        expanded.ConfirmCancel();

        Assert.Equal(1, heard.Count(h => h.Live && string.Equals(h.Text, Labels.SendingArrivesNote, StringComparison.Ordinal)));
        expanded.AskCancel();
        Assert.Equal((false, string.Empty), (expanded.PendingVoice.IsAnnouncing, expanded.PendingNote));
        Assert.DoesNotContain(heard, h => h.Text.Length == 0);
    }
}
