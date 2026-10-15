using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>The palette's announcements of a run it started (<see cref="CommandPaletteViewModelTests"/>, continued): retries, copies, and what is said once.</summary>
public sealed partial class CommandPaletteViewModelTests
{
    private PaletteEntry SelectRebuild()
    {
        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        return _palette.Selected!;
    }

    // K1 (run 5, a11y-0): a retry with the same outcome inside the window is a new announcement. The text the live region holds is the
    // same as the last run's, so the palette rests the region on a neutral text first (the gate dark) and then says it.
    [Fact]
    public async Task A_retry_with_the_same_refusal_is_spoken_again()
    {
        _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005", "failed");
        var rebuild = SelectRebuild();
        var spoken = Spoken();
        var live = new List<string>();
        _palette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.Announcement) && _palette.IsAnnouncing)
            {
                live.Add(_palette.Announcement);
            }
        };

        await _palette.Run(rebuild, Token);
        await _palette.Run(rebuild, Token);

        Assert.Equal(2, spoken.Count(t => t.Contains("SND-API-005: failed", StringComparison.Ordinal)));
        Assert.Equal(2, live.Count(t => t.Contains("SND-API-005: failed", StringComparison.Ordinal)));
        Assert.DoesNotContain("Command palette", live, StringComparer.Ordinal);   // the neutral text it rests on is never spoken: the region is dark for it
    }

    [Fact]
    public async Task A_retry_that_is_accepted_inside_the_two_seconds_is_spoken_again()
    {
        var rebuild = SelectRebuild();
        var spoken = Spoken();

        await _palette.Run(rebuild, Token);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        await _palette.Run(rebuild, Token);

        Assert.Equal(2, spoken.Count(t => t.StartsWith("Rebuild views: done", StringComparison.Ordinal)));
    }

    // K1 (a11y-2): a second Copy inside the 1.5 s says "Copied" again, from the palette; the control stays dark.
    [Fact]
    public async Task A_second_copy_inside_the_window_is_spoken_again_by_the_palette()
    {
        _palette.Open();
        _palette.Query = "approve profile";
        var entry = _palette.Selected!;
        var spoken = Spoken();

        await _palette.Run(entry, Token);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        await _palette.Run(entry, Token);

        Assert.Equal(2, spoken.Count(t => t.Contains("Copied", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_second_copy_inside_the_window_is_spoken_again_by_the_control()
    {
        var control = _controls.For("ApproveProfile")!;
        var said = new List<string>();
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlViewModel.Announcement) && control.IsAnnouncing)
            {
                said.Add(control.Announcement);
            }
        };

        await control.Copy(Token);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        await control.Copy(Token);

        Assert.Equal(2, said.Count(t => t == "Copied"));
    }

    // K3 (run 5, corr-1): "Sending…" is spoken once. When the selection moved off the entry after it was said, the outcome's own changes
    // (the outcome arrives before the phase does) must not say the entry's name and "Sending…" again.
    [Fact]
    public async Task Sending_is_spoken_once_when_the_selection_moved_off_after_it_was_said()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var rebuild = SelectRebuild();
        var spoken = Spoken();

        var run = _palette.Run(rebuild, Token);
        _clock.Advance(ControlViewModel.SendingAfter);
        _palette.Move(1);
        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal(1, spoken.Count(t => t.Contains("Sending…", StringComparison.Ordinal)));
        Assert.Contains(spoken, t => t.StartsWith("Rebuild views: done", StringComparison.Ordinal));
    }

    // K4 (run 5, corr-2): a send that is cancelled leaves the phase Idle, so the palette's finally really lets go of the control.
    [Fact]
    public async Task A_cancelled_palette_send_leaves_the_control_idle_and_unmuted()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var rebuild = SelectRebuild();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = _palette.Run(rebuild, stop.Token);
        await stop.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Equal((false, CommandPhase.Idle, true), (rebuild.Control!.IsMuted, rebuild.Control.Phase, rebuild.IsEnabled));
    }
}
