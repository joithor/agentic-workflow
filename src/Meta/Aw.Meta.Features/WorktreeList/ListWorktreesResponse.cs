using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Features.WorktreeList;

/// <summary>The ListWorktrees response.</summary>
/// <param name="Worktrees">Every worktree, main first, with its verdict and reasons.</param>
public sealed record ListWorktreesResponse(IReadOnlyList<WorktreeReport> Worktrees);
