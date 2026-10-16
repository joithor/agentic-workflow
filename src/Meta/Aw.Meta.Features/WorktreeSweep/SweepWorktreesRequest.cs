namespace Aw.Meta.Features.WorktreeSweep;

/// <summary>The SweepWorktrees request: remove the removable worktrees.</summary>
/// <param name="DryRun">Report what would be removed; remove nothing.</param>
/// <param name="Pr">Only the worktrees of this pull request (its head branch and its gate trees); null for every worktree.</param>
public sealed record SweepWorktreesRequest(bool DryRun = false, int? Pr = null);
