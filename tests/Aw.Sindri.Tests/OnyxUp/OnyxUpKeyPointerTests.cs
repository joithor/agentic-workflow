using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The key pointer lives in state.json beside the step stamps: a stack step rewriting the file must not lose it (the next run would ask for the login again and make a second key).
public sealed class OnyxUpKeyPointerTests
{
    private const string Pointer = "keychain:onyx/sindri-onyx";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<OnyxUpRig> RanOnce()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_reruns_and_rewrites_state_keeps_the_key_pointer()
    {
        var rig = await RanOnce();
        var steps = rig.Workspace.State!.Steps.Where(static s => s.Key != "config").ToDictionary(static s => s.Key, static s => s.Value, StringComparer.Ordinal);
        rig.Workspace.State = rig.Workspace.State with { Steps = steps, KeyPointer = Pointer };
        var writes = rig.Workspace.StateWrites;

        var rerun = (await rig.Up(Token)).Value;

        Assert.Contains("config", rerun.Ran, StringComparer.Ordinal);
        Assert.True(rig.Workspace.StateWrites > writes);
        Assert.Equal(Pointer, rig.Workspace.State!.KeyPointer);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_state_another_home_wrote_does_not_hand_its_key_pointer_to_this_one()
    {
        var rig = await RanOnce();
        rig.Workspace.State = rig.Workspace.State! with { Project = "sindri-onyx-ffffffff", KeyPointer = Pointer };

        _ = (await rig.Up(Token)).Value;

        Assert.Null(rig.Workspace.State!.KeyPointer);
    }
}
