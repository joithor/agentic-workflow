using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Features.RecordRun;

/// <summary>The result of record-run.</summary>
/// <param name="Candidate">The candidate.</param>
/// <param name="Passed">Whether the check passed.</param>
/// <param name="Next">What to do next.</param>
public sealed record RecordRunResponse(
    CandidateId Candidate,
    bool Passed,
    string Next);
