using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxEnsure;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxEnsure;

// When the managed Onyx needs the human, and the needs-you row it becomes.
public sealed class OnyxNoticeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, false, false, null, false)]    // nothing configured, nobody declined: no row
    [InlineData(true, false, false, null, true)]      // managed profile, never set up
    [InlineData(false, false, true, null, true)]      // the human said no to the offer
    [InlineData(true, true, false, "running", false)] // set up and running
    [InlineData(true, true, false, "failed: no docker", true)]
    [InlineData(false, true, false, "failed: no docker", false)] // an unmanaged Onyx is not ours to nag about
    [InlineData(true, true, true, null, false)]       // set up wins over a stale decline
    public void The_rules_say_when_the_human_is_needed(bool managed, bool setUp, bool declined, string? ensure, bool expected)
        => Assert.Equal(expected, OnyxNoticeRules.Decide(managed, setUp, declined, ensure) is not null);

    [Fact, Trait("Lane", "unit")]
    public void A_setup_notice_names_the_command_and_a_failure_names_why()
    {
        Assert.Equal("sindri onyx up", OnyxNoticeRules.Decide(managed: true, setUp: false, declined: false, ensure: null)!.Command);
        Assert.Contains("no docker", OnyxNoticeRules.Decide(managed: true, setUp: true, declined: false, "failed: no docker")!.Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_row_is_a_repo_less_failure_row_for_onyx_with_the_command_and_no_action()
    {
        var notice = OnyxNoticeRules.Decide(managed: true, setUp: false, declined: false, ensure: null)!;

        var row = NeedsYouRows.ForOnyx(notice, DateTimeOffset.UnixEpoch, new PassThroughScrubber()).Value;

        Assert.Equal(NeedsYouKind.Failure, row.Kind);
        Assert.Equal("sindri onyx up", row.Cli);
        Assert.Null(row.Repo);
        Assert.Null(row.PrimaryAction);
        Assert.Empty(row.Actions);
        Assert.Equal("Set up Onyx", row.Title);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_service_reads_the_profile_the_state_the_declined_marker_and_serves_note()
    {
        var rig = new OnyxUpRig();
        var managed = new FakeManagedOnyx();
        var notices = new OnyxNotices(managed, rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)));
        Assert.Null((await notices.Current(Token)).Value);

        managed.Managed = true;
        Assert.Equal("Set up Onyx", (await notices.Current(Token)).Value!.Title);

        rig.Workspace.State = new OnyxSetupState(3000, new Dictionary<string, string>(StringComparer.Ordinal) { [OnyxSteps.Start] = "x" });
        Assert.Null((await notices.Current(Token)).Value);

        rig.Workspace.Files[rig.Paths.Ensure] = "failed: Docker isn't running\n";
        Assert.Equal("Onyx didn't start", (await notices.Current(Token)).Value!.Title);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_declined_offer_leaves_a_row_until_onyx_is_set_up()
    {
        var rig = new OnyxUpRig();
        var notices = new OnyxNotices(new FakeManagedOnyx(), rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)));
        rig.Workspace.Files[rig.Paths.Declined] = string.Empty;

        Assert.NotNull((await notices.Current(Token)).Value);
    }
}
