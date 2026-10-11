namespace Aw.BugFix.Features.RecordCandidate;

/// <summary>The record-candidate: record a worktree's fix commit as a candidate.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Branch">The worktree's branch.</param>
/// <param name="WorkingDirectory">The worktree.</param>
/// <param name="Hypothesis">The hypothesis it pursues (required in mode B).</param>
/// <param name="AllowTestChanges">Whether the user approved changes to tests, fixtures or runner config.</param>
public sealed record RecordCandidateRequest(
    string StateDirectory,
    string Branch,
    string WorkingDirectory,
    int? Hypothesis,
    bool AllowTestChanges);
