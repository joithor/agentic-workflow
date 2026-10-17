using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IRunDiff"/> honours (engine P8 D10): a run changed nothing while HEAD is the start HEAD and the
/// worktree has no change; a new file or a new commit is a change.
/// </summary>
public abstract class RunDiffContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The adapter and a worktree with one commit.</summary>
    /// <returns>The adapter and the worktree's path.</returns>
    protected abstract ValueTask<(IRunDiff Diff, string Worktree)> Create();

    /// <summary>Adds an untracked file to <paramref name="worktree"/>.</summary>
    /// <param name="worktree">The worktree.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask WriteFile(string worktree);

    /// <summary>Commits on top of HEAD in <paramref name="worktree"/>.</summary>
    /// <param name="worktree">The worktree.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask Commit(string worktree);

    /// <summary>A worktree nobody touched is empty.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_untouched_worktree_is_empty()
    {
        var (diff, worktree) = await Create();
        var start = await diff.Head(worktree, Token);

        var empty = await diff.IsEmpty(worktree, start.Value, Token);

        Expect.True(start.IsSuccess && empty.IsSuccess && empty.Value);
    }

    /// <summary>A new file is a change.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_file_is_a_change()
    {
        var (diff, worktree) = await Create();
        var start = await diff.Head(worktree, Token);
        await WriteFile(worktree);

        var empty = await diff.IsEmpty(worktree, start.Value, Token);

        Expect.True(empty.IsSuccess && !empty.Value);
    }

    /// <summary>A new commit is a change even when the worktree is clean afterwards.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_commit_is_a_change()
    {
        var (diff, worktree) = await Create();
        var start = await diff.Head(worktree, Token);
        await Commit(worktree);

        var empty = await diff.IsEmpty(worktree, start.Value, Token);
        var moved = await diff.Head(worktree, Token);

        Expect.True(empty.IsSuccess && !empty.Value);
        Expect.True(moved.IsSuccess && !string.Equals(moved.Value, start.Value, StringComparison.Ordinal));
    }

    /// <summary>A worktree that isn't there is an error, never "empty".</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_worktree_is_an_error()
    {
        var (diff, worktree) = await Create();

        var head = await diff.Head(Path.Combine(worktree, "no-such-dir"), Token);
        var empty = await diff.IsEmpty(Path.Combine(worktree, "no-such-dir"), "abc", Token);

        Expect.True(head.IsError && empty.IsError);
    }
}
