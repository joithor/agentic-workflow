using Aw.Sindri.Core;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// A rerun on a box that is already set up changes nothing, so the space its own images use is no reason to stop it; and a directory Sindri can't write is an error, not a crash.
public sealed class OnyxUpRerunTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_finished_up_answers_the_failed_note_serve_left()
    {
        var rig = new OnyxUpRig();
        rig.Workspace.Files[rig.Paths.Ensure] = "failed: Docker isn't running\n";

        _ = (await rig.Up(Token)).Value;

        Assert.Equal("running", rig.Workspace.Files[rig.Paths.Ensure].Trim());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_after_the_images_are_pulled_warns_about_low_disk_and_carries_on()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Probes.FreeBytes = 1L << 30;   // the images now use the space the preflight asked for

        var again = await rig.Up(Token);

        Assert.True(again.IsSuccess);
        Assert.Contains(rig.Progress.Lines, static l => l.StartsWith("disk:", StringComparison.Ordinal) && l.Contains("isn't blocking", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Low_disk_before_the_images_are_pulled_still_stops_the_run()
    {
        var rig = new OnyxUpRig();
        rig.Probes.FreeBytes = 1L << 30;

        var up = await rig.Up(Token);

        Assert.True(up.IsError);
        Assert.Equal(SindriErrors.OnyxNotEnoughResources.Code, up.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_workspace_that_cannot_be_written_is_sindri_onyx_012_not_a_crash()
    {
        var rig = new OnyxUpRig();
        rig.Workspace.WritesFail = true;

        var up = await rig.Up(Token);

        Assert.True(up.IsError);
        Assert.Equal(SindriErrors.OnyxWorkspaceUnusable.Code, up.Error.Code);
    }
}
