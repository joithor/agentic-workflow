using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Settings' Reduce motion reaches the Needs-you rows the way the text scale does (proof B5; spec §11.4): an outline and "new", no pulse.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellReduceMotionTests
{
    private readonly InMemoryPreferencesStore _store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private (ShellViewModel Shell, SettingsViewModel Settings) Wired()
    {
        var preferences = new Preferences(_store);
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), preferences, new FakeClipboard());
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Park()], ""));
        return (shell, new SettingsViewModel(preferences, new FakeMotionPreference(), shell.ApplyAppearance));
    }

    [Fact]
    public async Task Turning_Reduce_motion_on_stops_the_pulse_and_draws_the_outline_and_the_word_new()
    {
        var (shell, settings) = Wired();
        await settings.Load(Token);
        var row = shell.NeedsYou.Rows[0];
        Assert.Equal((true, false, Labels.New), (row.IsPulsing, row.HasOutline, row.NewWord));   // motion only adds to the word

        settings.ReduceMotion = true;

        Assert.Equal((false, true, Labels.New), (row.IsPulsing, row.HasOutline, row.NewWord));
    }

    [Fact]
    public async Task Turning_it_off_again_restores_the_pulse()
    {
        var (shell, settings) = Wired();
        await settings.Load(Token);
        settings.ReduceMotion = true;

        settings.ReduceMotion = false;

        Assert.Equal((true, false), (shell.NeedsYou.Rows[0].IsPulsing, shell.NeedsYou.Rows[0].HasOutline));
    }

    [Fact]
    public async Task The_stored_switch_reaches_the_rows_when_settings_load()
    {
        await new Preferences(_store).Update(_ => UiPreferences.Defaults with { ReduceMotion = true });
        var (shell, settings) = Wired();

        await settings.Load(Token);

        Assert.True(shell.NeedsYou.Rows[0].HasOutline);
    }
}
