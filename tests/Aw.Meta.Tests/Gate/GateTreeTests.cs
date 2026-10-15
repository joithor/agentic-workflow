using Aw.Meta.Core;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Gate;

public sealed class GateTreeTests
{
    private const string Tree = "/state/meta/gates/190/tree";
    private readonly MetaWorld _world = new();

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_worktree_is_created_detached_and_then_checked_out_clean()
    {
        _ = _world.Runner.When("rev-parse --git-dir", exitCode: 128).When("git");

        var done = await _world.Trees.Prepare("/work", Tree, "origin/main");

        Assert.True(done.IsSuccess);
        Assert.Equal(
            [
                "git -C /state/meta/gates/190/tree rev-parse --git-dir",
                "git worktree add --force --detach /state/meta/gates/190/tree origin/main",
                "git checkout --quiet --force --detach origin/main",
                "git reset --quiet --hard",
                "git " + "clean --quiet -fd",
            ],
            _world.Runner.Lines,
            StringComparer.Ordinal);
        Assert.Equal(["/work", "/work", Tree, Tree, Tree], _world.Runner.Requests.Select(r => r.Cwd), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_existing_worktree_is_only_reset()
    {
        _ = _world.Runner.When("git");

        _ = await _world.Trees.Prepare("/work", Tree, "abc123");

        Assert.DoesNotContain(_world.Runner.Lines, line => line.Contains("worktree add", StringComparison.Ordinal));
        Assert.Contains("git checkout --quiet --force --detach abc123", _world.Runner.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_clean_merge_is_true()
    {
        _ = _world.Runner.When("git");

        var merged = await _world.Trees.Merge(Tree, "headsha");

        Assert.True(merged.Value);
        Assert.Contains("merge --quiet --no-edit headsha", _world.Runner.Lines[0], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_that_leaves_unmerged_paths_is_a_conflict_and_is_aborted()
    {
        _ = _world.Runner.When("merge --quiet", exitCode: 1).When("--diff-filter=U", "a.cs\n").When("git merge --abort");

        var merged = await _world.Trees.Merge(Tree, "headsha");

        Assert.False(merged.Value);
        Assert.Contains("git merge --abort", _world.Runner.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_that_fails_without_unmerged_paths_is_an_error()
    {
        _ = _world.Runner.When("merge --quiet", exitCode: 128).When("--diff-filter=U", "");

        var merged = await _world.Trees.Merge(Tree, "headsha");

        Assert.Equal(MetaErrors.CommandFailed.Code, merged.Error.Code);
    }
}
