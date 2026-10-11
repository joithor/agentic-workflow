using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Features.RecordCandidate;

/// <summary>The result of record-candidate.</summary>
/// <param name="Candidate">The new candidate.</param>
/// <param name="Commit">Its commit.</param>
/// <param name="ChangedFiles">Files changed since the baseline.</param>
public sealed record RecordCandidateResponse(
    CandidateId Candidate,
    string Commit,
    IReadOnlyList<string> ChangedFiles);
