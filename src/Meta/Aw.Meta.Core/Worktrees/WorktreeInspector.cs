using Aw.Kernel;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Worktrees;

/// <summary>Gathers the facts about every worktree of the team's repo and judges each by <see cref="WorktreeRules"/>.</summary>
public sealed class WorktreeInspector
{
    /// <summary>Inspects the worktrees in a scope.</summary>
    /// <param name="team">The team config (the repo, the worktree settings).</param>
    /// <param name="scope">Which worktrees to look at.</param>
    /// <param name="fetch">Whether to fetch origin first (a sweep always does; a read-only list may skip it).</param>
    /// <param name="onlyPath">When given, just that worktree (the re-check just before a removal).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One report per worktree, or an error when the repo, GitHub or the process scan can't be read (a sweep then removes nothing).</returns>
    public ValueTask<Result<IReadOnlyList<WorktreeReport>>> Inspect(TeamConfig team, WorktreeScope scope, bool fetch, string? onlyPath, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
