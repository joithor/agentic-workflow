namespace Aw.Sindri.Core.BugFix;

/// <summary>
/// bugfix-state as a port (deps.yaml Sindri to BugFix, kind cli): the four calls the bugfix block Steps make. bugfix-state owns the bug fix's state under <c language="text">stateDirectory</c>: the attempt count,
/// the candidate rules, the frozen check and the once-per-candidate verdict. A refusal is an answer; an unavailable tool is the other.
/// </summary>
public interface IBugFixState
{
    /// <summary>Opens the next attempt (at most 3).</summary>
    /// <param name="stateDirectory">The bug fix's state directory.</param>
    /// <param name="mode">The attempt's mode.</param>
    /// <param name="cancellationToken">Cancels the call: the tool is killed.</param>
    /// <returns>The attempt; a refusal of kind AttemptCap when the cap is used up.</returns>
    public ValueTask<BugFixAnswer<AttemptStarted>> StartAttempt(string stateDirectory, AttemptMode mode, CancellationToken cancellationToken);

    /// <summary>Records the worktree's commit as the next candidate of the attempt.</summary>
    /// <param name="stateDirectory">The bug fix's state directory.</param>
    /// <param name="source">The candidate's branch and worktree.</param>
    /// <param name="cancellationToken">Cancels the call: the tool is killed.</param>
    /// <returns>The candidate.</returns>
    public ValueTask<BugFixAnswer<CandidateRecorded>> RecordCandidate(string stateDirectory, CandidateSource source, CancellationToken cancellationToken);

    /// <summary>Runs the frozen check on the candidate's worktree and records the run for the candidate.</summary>
    /// <param name="stateDirectory">The bug fix's state directory.</param>
    /// <param name="candidate">The candidate's id.</param>
    /// <param name="worktree">The candidate's worktree.</param>
    /// <param name="check">The frozen check.</param>
    /// <param name="cancellationToken">Cancels the call: the tool is killed.</param>
    /// <returns>The run, passed or failed; a refusal when the run cannot be taken (a dirty tree, a changed check, a broken run).</returns>
    public ValueTask<BugFixAnswer<CheckRun>> Recheck(string stateDirectory, string candidate, string worktree, FrozenCheck check, CancellationToken cancellationToken);

    /// <summary>Asks judge resolution-check about the candidate, once: bugfix-state refuses a second ask and a candidate without a passing run.</summary>
    /// <param name="stateDirectory">The bug fix's state directory.</param>
    /// <param name="candidate">The candidate's id.</param>
    /// <param name="cancellationToken">Cancels the call: the tool is killed.</param>
    /// <returns>The recorded verdict; a refusal of kind NoPassingRun when the check did not pass.</returns>
    public ValueTask<BugFixAnswer<ResolutionVerdict>> Judge(string stateDirectory, string candidate, CancellationToken cancellationToken);
}
