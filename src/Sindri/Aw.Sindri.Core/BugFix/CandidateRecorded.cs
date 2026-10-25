namespace Aw.Sindri.Core.BugFix;

/// <summary>A candidate bugfix-state recorded.</summary>
/// <param name="Candidate">c1, c2, … in recording order.</param>
/// <param name="Commit">The candidate commit.</param>
/// <param name="Attempt">The attempt it belongs to.</param>
/// <param name="ChangedFiles">Files changed since the baseline.</param>
public sealed record CandidateRecorded(string Candidate, string Commit, int Attempt, IReadOnlyList<string> ChangedFiles);
