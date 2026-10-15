using Aw.Context.Core.Proposals;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a proposal run wrote.</summary>
/// <param name="Result">The draft and the counts: added, already mapped, rejected earlier.</param>
/// <param name="Sequence">The 'context.map.proposed' event's ledger sequence, or null when nothing was added.</param>
public sealed record ProposeContextMapResponse(ProposeResult Result, long? Sequence);
