using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>'effect.pr.push' (PB spec §10.1, W26): the workspace head goes to the PR head under a lease on the head the snapshot saw; a moved head is 'headMoved', never a force.</summary>
[Lane(TestLane.Unit)]
public sealed class PrPushEffectTests
{
    private readonly PrEffectRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pushing_a_test_only_commit_replies_pushed_with_the_lease_and_the_fact()
    {
        _rig.Diff.Touch(PrEffectRig.Workspace, "tests/Aw.X.Tests/FooTests.cs");
        _rig.Diff.Advance(PrEffectRig.Workspace);

        var done = await Dispatch();

        Assert.Equal("pushed", done.Reply!.Value.Value);
        var push = Assert.Single(_rig.Forge.Pushes);
        Assert.Equal(new PullPush(_rig.Link, PrEffectRig.Workspace, "aaa111+", PrEffectRig.Seen), push);
        Assert.Equal("aaa111+", _rig.Pull.HeadSha);
        var pushed = await Pushed(done);
        Assert.False(pushed.GetProperty("source").GetBoolean());
        Assert.Equal("aaa111+", pushed.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task A_commit_that_touches_source_replies_pushedSource()
    {
        _rig.Diff.Touch(PrEffectRig.Workspace, "src/Aw.X/Foo.cs", "tests/Aw.X.Tests/FooTests.cs");
        _rig.Diff.Advance(PrEffectRig.Workspace);

        var done = await Dispatch();

        Assert.Equal("pushedSource", done.Reply!.Value.Value);
        Assert.True((await Pushed(done)).GetProperty("source").GetBoolean());
    }

    [Fact]
    public async Task A_workspace_still_at_the_seen_head_pushes_nothing()
    {
        var done = await Dispatch();

        Assert.Equal("nothing", done.Reply!.Value.Value);
        Assert.Empty(_rig.Forge.Pushes);
    }

    [Fact]
    public async Task A_head_that_moved_replies_headMoved_and_leaves_the_branch_alone()
    {
        _rig.Diff.Advance(PrEffectRig.Workspace);
        _rig.Pull.HeadSha = "someone-else";

        var done = await Dispatch();

        Assert.Equal("headMoved", done.Reply!.Value.Value);
        Assert.Equal("someone-else", _rig.Pull.HeadSha);
    }

    [Fact]
    public async Task A_dispatch_repeated_after_the_push_landed_is_found_already_pushed()
    {
        _rig.Diff.Advance(PrEffectRig.Workspace);
        _ = await Dispatch();

        var again = await Dispatch();

        Assert.Equal("pushed", again.Reply!.Value.Value);
        Assert.True(again.Existed);
    }

    [Fact]
    public async Task A_forge_error_is_the_dispatch_error()
    {
        _rig.Diff.Advance(PrEffectRig.Workspace);
        var effect = new PrPushEffect(new Aw.Sindri.Testing.Forge.FakePullRequests([]), _rig.Diff, _rig.Facts, _rig.Facts);

        var result = await effect.Dispatch(Context(), Token);

        Assert.True(result.IsError);
    }

    private PrPushEffect Effect() => new(_rig.Forge, _rig.Diff, _rig.Facts, _rig.Facts);

    private Aw.Sindri.Core.Workflow.Blocks.EffectContext Context() => _rig.Context("effect.pr.push", new Dictionary<string, object>(StringComparer.Ordinal)
    {
        ["pr"] = PrEffectRig.Fact($"\"{_rig.Link}\""),
        ["lease"] = PrEffectRig.Fact(PrEffectRig.PrState(PrEffectRig.Seen, PrEffectRig.Seen)),
        ["workspace"] = PrEffectRig.Fact($"\"{PrEffectRig.Workspace}\""),
        ["cause"] = "ci",
    });

    private async Task<Aw.Sindri.Core.Workflow.Blocks.EffectDone> Dispatch() => (await Effect().Dispatch(Context(), Token)).Value;

    private async Task<JsonElement> Pushed(Aw.Sindri.Core.Workflow.Blocks.EffectDone done)
    {
        var reply = done.Facts![FactKey.From("fact:push")];
        var content = await _rig.Facts.Read(new Aw.Sindri.Core.Workflow.Engine.FactValue(reply.Ref, reply.Sha256, null), Token);
        return JsonDocument.Parse(content.Value).RootElement.Clone();
    }
}
