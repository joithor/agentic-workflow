using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Worktrees;

/// <summary>One worktree and what the rules say about it.</summary>
/// <param name="Path">The worktree's directory.</param>
/// <param name="Branch">The branch checked out; null when detached.</param>
/// <param name="Head">The HEAD commit.</param>
/// <param name="Pr">The pull request whose head branch this is, if one is known.</param>
/// <param name="PrState">That pull request's state.</param>
/// <param name="Decision">Removable or kept.</param>
/// <param name="Reasons">Every reason it is kept; empty when removable.</param>
public sealed record WorktreeReport(string Path, string? Branch, string Head, int? Pr, PrState? PrState, WorktreeDecision Decision, IReadOnlyList<string> Reasons);
