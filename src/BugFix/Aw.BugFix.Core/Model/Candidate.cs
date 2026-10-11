using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.Kernel;

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

    /// <summary>Whether judge may be asked about this candidate: once, and only after a passing run.</summary>
    /// <returns>The candidate, or AW-BUGFIX-015.</returns>
    public Result<Candidate> Judgeable()
    {
        if (Run is not { Passed: true })
        {
            return BugFixErrors.JudgeRefused.With($"{Id.Value} has no passing run; judge only runs after the check passes");
        }

        return Verdict is null
            ? this
            : BugFixErrors.JudgeRefused.With($"{Id.Value} was already judged ({Names.Of(Verdict.Decision)}); a verdict is never re-asked");
    }

    /// <summary>Whether the bug fix may be reported resolved by this candidate: only with a passing run and a resolved verdict.</summary>
    /// <returns>The candidate, or AW-BUGFIX-019.</returns>
    public Result<Candidate> ResolvesTicket()
    {
        if (Resolves)
        {
            return this;
        }

        var run = Run switch { null => "none", { Passed: true } => "passed", _ => "failed" };
        var verdict = Verdict is null ? "none" : Names.Of(Verdict.Decision);
        return BugFixErrors.ReportRefused.With($"{Id.Value} needs a passing run and a \"resolved\" judge decision (run: {run}, judge: {verdict})");
    }

    /// <summary>Gets a value indicating whether the candidate needs nothing more: a failing run, or a passing run with a verdict.</summary>
    public bool Evaluated => Run is not null && (!Run.Passed || Verdict is not null);
}
