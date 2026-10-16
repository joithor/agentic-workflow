namespace Aw.Meta.Features.WorktreeList;

/// <summary>The ListWorktrees request: every worktree of the team repo with its verdict.</summary>
/// <param name="Fetch">Whether to fetch origin first; false judges against the origin refs as they are.</param>
public sealed record ListWorktreesRequest(bool Fetch = true);
