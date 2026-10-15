namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>What Enter does (<see cref="CommandPaletteViewModelTests"/>, continued): an entry that shows as runnable does something.</summary>
public sealed partial class CommandPaletteViewModelTests
{
    // K8 (Linux proof): IssueChallenge and RegisterRecipe showed "ready" and did nothing on Enter. An entry is enabled only when
    // the app can do what Enter means (send it, copy it, show its page), and each such entry does.
    [Fact]
    public async Task Every_entry_that_is_enabled_does_something_on_Enter()
    {
        _palette.Open();
        var enabled = _palette.Entries.Where(e => e.IsEnabled).ToList();
        Assert.NotEmpty(enabled);

        foreach (var entry in enabled)
        {
            _ = await _clipboard.SetText(string.Empty, Token);
            var before = (_pages.Count, _server.RebuildCalls);

            await _palette.Run(entry, Token);

            var did = (_pages.Count, _server.RebuildCalls) != before || (entry.Control is { } control && string.Equals(_clipboard.Text, control.Cli, StringComparison.Ordinal));
            Assert.True(did, $"Enter on '{entry.Label}' ({entry.Status}) did nothing");
            _palette.Open();
            _clock.Advance(TimeSpan.FromSeconds(3));
        }
    }

    // A ready row the app has no sender for is not "ready": it says so, and Enter skips it.
    [Fact]
    public void A_ready_row_without_a_sender_is_not_called_ready()
    {
        _palette.Open();

        var challenge = Entry("IssueChallenge");

        Assert.Equal((false, "not in the app yet"), (challenge.IsEnabled, challenge.Status));
        Assert.Equal("ready", Entry("RebuildViews").Status);
        Assert.Equal("ready", Entry("StopProcess").Status);   // it goes to Health, where its confirm lives
    }
}
