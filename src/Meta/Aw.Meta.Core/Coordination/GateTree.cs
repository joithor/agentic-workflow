using Aw.Kernel;

namespace Aw.Meta.Core.Coordination;

/// <summary>The gate's git worktrees: a detached checkout of origin/main that a pull request's head is merged onto.</summary>
/// <param name="repo">The git calls.</param>
public sealed class GateTree(RepoCommands repo)
{
    /// <summary>The same worktree handling with every git call bound to a token.</summary>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>A copy bound to the token.</returns>
    public GateTree Cancelling(CancellationToken cancellationToken) => new(repo.Cancelling(cancellationToken));

    private static readonly string[] _identity = ["-c", "user.name=aw-meta", "-c", "user.email=aw-meta@localhost"];

    /// <summary>Checks the tree out at <paramref name="revision"/>, creating the worktree on first use and discarding anything left in it.</summary>
    /// <param name="repoDirectory">The shared checkout the worktree belongs to.</param>
    /// <param name="tree">The worktree's directory.</param>
    /// <param name="revision">What to check out, detached (origin/main).</param>
    /// <returns>Success, or AW-META-012.</returns>
    public async ValueTask<Result<Unit>> Prepare(string repoDirectory, string tree, string revision)
    {
        ArgumentNullException.ThrowIfNull(repoDirectory);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(revision);
        var exists = await repo.Git(repoDirectory, "-C", tree, "rev-parse", "--git-dir").ConfigureAwait(false);
        var made = exists.IsSuccess ? Results.Ok() : (await repo.Git(repoDirectory, "worktree", "add", "--force", "--detach", tree, revision).ConfigureAwait(false)).Map(static _ => Unit.Value);
        if (made.IsError)
        {
            return made.Error;
        }

        foreach (var step in (string[][])[["checkout", "--quiet", "--force", "--detach", revision], ["reset", "--quiet", "--hard"], ["clean", "--quiet", "-fd"]])
        {
            var done = await repo.Git(tree, step).ConfigureAwait(false);
            if (done.IsError)
            {
                return done.Error;
            }
        }

        return Results.Ok();
    }

    /// <summary>Merges a commit into the tree.</summary>
    /// <param name="tree">The worktree's directory.</param>
    /// <param name="head">The commit to merge.</param>
    /// <returns>True when merged; false on a conflict (the merge is aborted); or AW-META-012.</returns>
    public async ValueTask<Result<bool>> Merge(string tree, string head)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(head);
        var merged = await repo.Git(tree, [.. _identity, "merge", "--quiet", "--no-edit", head]).ConfigureAwait(false);
        if (merged.IsSuccess)
        {
            return true;
        }

        var unmerged = await repo.Git(tree, "diff", "--name-only", "--diff-filter=U").ConfigureAwait(false);
        if (unmerged.IsError || unmerged.Value.Length == 0)
        {
            return merged.Error;
        }

        _ = await repo.Git(tree, "merge", "--abort").ConfigureAwait(false);
        return false;
    }

    /// <summary>Merges commits into the tree one after the other.</summary>
    /// <param name="tree">The worktree's directory.</param>
    /// <param name="heads">The commits to merge, in order.</param>
    /// <returns>True when all merged; false on the first conflict (that merge is aborted); or AW-META-012.</returns>
    public async ValueTask<Result<bool>> MergeAll(string tree, IReadOnlyList<string> heads)
    {
        ArgumentNullException.ThrowIfNull(heads);
        foreach (var head in heads)
        {
            var merged = await Merge(tree, head).ConfigureAwait(false);
            if (merged.IsError || !merged.Value)
            {
                return merged;
            }
        }

        return true;
    }
}
