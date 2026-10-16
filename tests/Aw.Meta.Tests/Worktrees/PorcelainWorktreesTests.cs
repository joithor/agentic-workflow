using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

public sealed class PorcelainWorktreesTests
{
    private static readonly string _output = string.Join(
        "\n",
        [
            "worktree /home/u/repos/app",
            "HEAD 1111111111111111111111111111111111111111",
            "branch refs/heads/main",
            "",
            "worktree /home/u/repos/aw-wt/feat-x",
            "HEAD 2222222222222222222222222222222222222222",
            "branch refs/heads/feat/x",
            "",
            "worktree /home/u/.aw/meta/gates/7/tree",
            "HEAD 3333333333333333333333333333333333333333",
            "detached",
            "",
            "worktree /home/u/repos/aw-wt/held",
            "HEAD 4444444444444444444444444444444444444444",
            "branch refs/heads/held",
            "locked usb disk",
            "",
            "worktree /home/u/repos/aw-wt/gone",
            "HEAD 5555555555555555555555555555555555555555",
            "branch refs/heads/gone",
            "locked",
            "prunable gitdir file points to non-existent location",
        ]);

    [Fact, Trait("Lane", "unit")]
    public void Each_block_is_a_worktree_and_the_first_is_the_main_one()
    {
        var entries = PorcelainWorktrees.Parse(_output);

        Assert.Equal(["/home/u/repos/app", "/home/u/repos/aw-wt/feat-x", "/home/u/.aw/meta/gates/7/tree", "/home/u/repos/aw-wt/held", "/home/u/repos/aw-wt/gone"], entries.Select(e => e.Path), StringComparer.Ordinal);
        Assert.Equal([true, false, false, false, false], entries.Select(e => e.IsMain));
        Assert.Equal(["main", "feat/x", null, "held", "gone"], entries.Select(e => e.Branch), StringComparer.Ordinal);
        Assert.Equal("2222222222222222222222222222222222222222", entries[1].Head);
    }

    [Fact, Trait("Lane", "unit")]
    public void Locks_and_prunable_entries_are_read()
    {
        var entries = PorcelainWorktrees.Parse(_output);

        Assert.Equal((true, "usb disk", false), (entries[3].Locked, entries[3].LockReason, entries[3].Prunable));
        Assert.Equal((true, null, true), (entries[4].Locked, entries[4].LockReason, entries[4].Prunable));
        Assert.False(entries[1].Locked);
    }

    [Fact, Trait("Lane", "unit")]
    public void Windows_line_endings_and_empty_output_are_tolerated()
    {
        Assert.Empty(PorcelainWorktrees.Parse(string.Empty));
        Assert.Equal("a", Assert.Single(PorcelainWorktrees.Parse("worktree /r\r\nHEAD abc\r\nbranch refs/heads/a\r\n")).Branch);
    }
}
