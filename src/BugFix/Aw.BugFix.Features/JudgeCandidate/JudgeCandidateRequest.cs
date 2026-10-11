using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Features.JudgeCandidate;

/// <summary>The judge: ask judge resolution-check once about a candidate with a passing run.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Candidate">The candidate.</param>
public sealed record JudgeCandidateRequest(
    string StateDirectory,
    CandidateId Candidate);
