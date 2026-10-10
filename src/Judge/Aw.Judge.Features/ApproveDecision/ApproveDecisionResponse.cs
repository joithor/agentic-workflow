using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Features.ApproveDecision;

/// <summary>The ApproveDecision response.</summary>
/// <param name="Decision">The decision, its reason code now prefixed 'approved:'.</param>
public sealed record ApproveDecisionResponse(DecisionRecord Decision);
