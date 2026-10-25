namespace Aw.Sindri.Core.BugFix;

/// <summary>The verdict bugfix-state recorded for a candidate.</summary>
/// <param name="Candidate">The candidate.</param>
/// <param name="Decision">judge's decision.</param>
/// <param name="ReasonCode">judge's reason code.</param>
/// <param name="Reasons">judge's reasons.</param>
public sealed record ResolutionVerdict(string Candidate, JudgeDecision Decision, string ReasonCode, IReadOnlyList<string> Reasons);
