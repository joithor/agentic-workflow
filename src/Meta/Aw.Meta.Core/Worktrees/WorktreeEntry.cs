namespace Aw.Meta.Core.Worktrees;

/// <summary>One worktree as "git worktree list --porcelain" reports it.</summary>
/// <param name="Path">The worktree's directory.</param>
/// <param name="Head">The commit its HEAD points at.</param>
/// <param name="Branch">The branch checked out, or null when HEAD is detached (or the entry is bare).</param>
/// <param name="IsMain">Whether it is the main worktree (git lists it first).</param>
/// <param name="Locked">Whether "git worktree lock" was run on it.</param>
/// <param name="LockReason">The lock's reason, when one was given.</param>
/// <param name="Prunable">Whether git says its directory is gone.</param>
internal sealed record WorktreeEntry(string Path, string Head, string? Branch, bool IsMain, bool Locked, string? LockReason, bool Prunable);
