using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Features.RecordRun;

/// <summary>The record-run: record the frozen check's run against a candidate.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Candidate">The candidate.</param>
/// <param name="Evidence">The run record (from run-test or run-ui).</param>
public sealed record RecordRunRequest(
    string StateDirectory,
    CandidateId Candidate,
    string Evidence);
