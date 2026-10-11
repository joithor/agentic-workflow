using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.JudgeCandidate;

/// <summary>The result of judge.</summary>
/// <param name="Candidate">The candidate.</param>
/// <param name="Decision">judge's decision.</param>
/// <param name="DecisionId">The stored decision's id; null for an escalation.</param>
/// <param name="ReasonCode">judge's reason code.</param>
/// <param name="Reasons">judge's reasons.</param>
/// <param name="Status">The bug fix's status after the verdict (needs-human after an escalation).</param>
public sealed record JudgeCandidateResponse(
    CandidateId Candidate,
    JudgeDecision Decision,
    JudgeDecisionId? DecisionId,
    string ReasonCode,
    IReadOnlyList<string> Reasons,
    BugFixStatus Status);
