using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The System page's connection states (system spec §3, slice 5): a box for Not running, Different versions and Unreadable, skeleton rows while Connecting, and the amber line over a stale answer.</summary>
[Lane(TestLane.Unit)]
public sealed class HealthStatesTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    [Fact]
    public async Task A_running_answer_shows_the_cards_and_no_box()
    {
        var system = new SystemTestHarness();

        await system.Show();

        Assert.Equal((false, false, false, true), (system.Vm.HasStateBox, system.Vm.IsConnectingPage, system.Vm.ShowStaleLine, system.Vm.ShowOverviewCards));
    }

    [Fact]
    public void Connecting_shows_the_word_and_skeleton_rows_in_place_of_the_cards()
    {
        var system = new SystemTestHarness();

        Assert.Equal((Labels.Connecting, true, false, false), (system.Vm.Sentence, system.Vm.IsConnectingPage, system.Vm.ShowOverviewCards, system.Vm.HasStateBox));
    }

    [Fact]
    public void Not_running_is_a_box_with_the_command_that_starts_sindri()
    {
        var system = new SystemTestHarness();

        _ = system.Vm.NotRunning("nothing answered");

        Assert.Equal(("Sindri is not running", "sindri serve", true, false, false), (system.Vm.StateBoxTitle, system.Vm.StateBoxFix, system.Vm.HasStateBoxFix, system.Vm.IsConnectingPage, system.Vm.ShowOverviewCards));
        Assert.True(system.Vm.HasStateBox);
        Assert.Empty(system.Vm.StateBoxLines);
    }

    [Fact]
    public void Different_versions_name_this_apps_api_and_sindris()
    {
        var system = new SystemTestHarness();

        system.Vm.Show(_api, A.Status() with { ApiVersion = SindriApiClient.AppApiVersion + 1 });

        Assert.Equal([(Labels.ThisApp, Labels.ApiVersion(SindriApiClient.AppApiVersion)), (Labels.Sindri, Labels.ApiVersion(SindriApiClient.AppApiVersion + 1))], system.Vm.StateBoxLines.Select(l => (l.Label, l.Value)));
        Assert.Equal((Labels.VersionMismatch, false, false), (system.Vm.StateBoxTitle, system.Vm.HasStateBoxFix, system.Vm.ShowOverviewCards));
    }

    [Fact]
    public void An_answer_the_app_cannot_read_says_so_and_keeps_the_served_code()
    {
        var system = new SystemTestHarness();

        _ = system.Vm.Failed(new Error("SND-UI-003", ErrorKind.Invalid, "unknown field"), _api);

        Assert.Equal(("Sindri is running, but this app can't read its answer", false), (system.Vm.StateBoxTitle, system.Vm.ShowOverviewCards));
        var line = Assert.Single(system.Vm.StateBoxLines);
        Assert.Contains("SND-UI-003", line.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Thirty_quiet_seconds_put_an_amber_line_over_the_last_answer_and_an_event_takes_it_away()
    {
        var system = new SystemTestHarness();
        await system.Show();

        system.Clock.Advance(HealthViewModel.StaleAfter);
        system.Vm.Age();

        Assert.Equal((true, "Showing the last answer. Sindri has not answered for 30 seconds.", true, false), (system.Vm.ShowStaleLine, system.Vm.StaleLine, system.Vm.ShowOverviewCards, system.Vm.HasStateBox));

        system.Vm.Answered();

        Assert.Equal((false, string.Empty), (system.Vm.ShowStaleLine, system.Vm.StaleLine));
    }
}
