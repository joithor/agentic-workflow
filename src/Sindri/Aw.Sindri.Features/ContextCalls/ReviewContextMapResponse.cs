using Aw.Context.Core.Review;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a review action showed or did.</summary>
/// <param name="Result">The rows written and what is still undecided, or null for a protected preview.</param>
/// <param name="Sequence">The 'context.map.reviewed' event's ledger sequence, or null (a preview, or no row written).</param>
/// <param name="Challenge">A protected preview's challenge, or null after the action.</param>
/// <param name="Summary">A protected preview's words: what the action would do.</param>
public sealed record ReviewContextMapResponse(ReviewResult? Result, long? Sequence, ContextChallenge? Challenge = null, string? Summary = null);
