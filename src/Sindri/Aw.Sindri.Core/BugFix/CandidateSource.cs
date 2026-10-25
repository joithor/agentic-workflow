namespace Aw.Sindri.Core.BugFix;

/// <summary>Where a fix candidate is: the worktree's branch and path, and what bugfix-state needs to accept it.</summary>
/// <param name="Branch">The worktree's branch.</param>
/// <param name="Worktree">The worktree's full path.</param>
/// <param name="Hypothesis">The hypothesis number it pursues, when named.</param>
/// <param name="AllowTestChanges">Whether the user approved a fix that touches the check, tests or runner config.</param>
public sealed record CandidateSource(string Branch, string Worktree, int? Hypothesis = null, bool AllowTestChanges = false);
