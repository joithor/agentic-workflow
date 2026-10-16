namespace Aw.Meta.Core.Worktrees;

/// <summary>A worktree's verdict: removable only when no rule gave a reason to keep it.</summary>
/// <param name="Decision">Removable or kept.</param>
/// <param name="Reasons">Every failing rule, in rule order; empty when removable.</param>
internal sealed record WorktreeVerdict(WorktreeDecision Decision, IReadOnlyList<string> Reasons);
