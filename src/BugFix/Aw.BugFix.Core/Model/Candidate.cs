using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Core.Model;

/// <summary>A fix candidate: a commit on top of the baseline, its check run and its judge verdict.</summary>
/// <param name="Id">c1, c2, … in recording order.</param>
/// <param name="Attempt">The attempt it belongs to (1-based).</param>
/// <param name="Mode">The attempt's mode.</param>
/// <param name="Branch">The worktree's branch.</param>
/// <param name="Worktree">The worktree's full path.</param>
/// <param name="Commit">The candidate commit.</param>
/// <param name="Hypothesis">The hypothesis number it pursues (required in mode B).</param>
/// <param name="ChangedFiles">Files changed since the baseline, as recorded.</param>
/// <param name="Run">The frozen check's run against it, once recorded.</param>
/// <param name="Verdict">judge's verdict, once asked.</param>
public sealed record Candidate(
    CandidateId Id,
    int Attempt,
    FixMode Mode,
    string Branch,
    string Worktree,
    string Commit,
    int? Hypothesis,
    IReadOnlyList<string> ChangedFiles,
    CandidateRun? Run,
    JudgeVerdict? Verdict)
{
    /// <summary>Gets a value indicating whether the candidate has a passing run and a resolved verdict.</summary>
    public bool Resolves => Run is { Passed: true } && Verdict?.Decision == JudgeDecision.Resolved;

    /// <summary>Gets a value indicating whether the candidate needs nothing more: a failing run, or a passing run with a verdict.</summary>
    public bool Evaluated => Run is not null && (!Run.Passed || Verdict is not null);
}
