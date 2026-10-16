using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Features.WorktreeSweep;

/// <summary>The SweepWorktrees response.</summary>
/// <param name="DryRun">Nothing was removed; Removed lists what would have been.</param>
/// <param name="Removed">The worktrees removed (or that would be).</param>
/// <param name="Kept">The worktrees kept, each with every reason.</param>
/// <param name="Failed">How many removals git refused (those are in Kept).</param>
public sealed record SweepWorktreesResponse(bool DryRun, IReadOnlyList<WorktreeReport> Removed, IReadOnlyList<WorktreeReport> Kept, int Failed);
