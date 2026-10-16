namespace Aw.Meta.Core.Worktrees;

/// <summary>What a sweep did.</summary>
/// <param name="DryRun">Nothing was removed; Removed lists what would have been (also true when the team config turns the triggers off).</param>
/// <param name="Removed">The worktrees removed, or with DryRun the ones that would be.</param>
/// <param name="Kept">The worktrees kept, each with every reason (a removal git refused is one).</param>
/// <param name="Failed">How many removals git refused.</param>
public sealed record SweepOutcome(bool DryRun, IReadOnlyList<WorktreeReport> Removed, IReadOnlyList<WorktreeReport> Kept, int Failed);
