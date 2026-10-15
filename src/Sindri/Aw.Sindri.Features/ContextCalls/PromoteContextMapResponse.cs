using Aw.Context.Core.Map;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a promote showed or did.</summary>
/// <param name="Draft">The draft, as answers name it ('v14@0a1b2c3').</param>
/// <param name="Promoted">The new current version (and the one it retired), or null for a preview.</param>
/// <param name="Sequence">The 'context.map.promoted' event's ledger sequence, or null for a preview.</param>
/// <param name="Challenge">A preview's challenge ('promote map v14'), or null after the promote.</param>
public sealed record PromoteContextMapResponse(string Draft, MapPromoted? Promoted, long? Sequence, ContextChallenge? Challenge = null);
