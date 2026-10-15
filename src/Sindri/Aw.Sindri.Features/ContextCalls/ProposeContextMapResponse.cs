using Aw.Context.Core.Proposals;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a proposal run wrote.</summary>
/// <param name="Result">The draft and the counts: added, already mapped, rejected earlier.</param>
/// <param name="Sequence">The 'context.map.proposed' event's ledger sequence, or null when the run has no propose rows for these surfaces, or the event for exactly these rows is already in the ledger (an exact repeat, a repeat after a promote, a return to an earlier set of surfaces). A retry after a lost append is the call that records it, so its sequence is a number.</param>
/// <param name="Surfaces">Where each surface of the run stands now, in the order it was submitted: mapped, new or unmapped, with its label and node key (spec §8.7).</param>
public sealed record ProposeContextMapResponse(ProposeResult Result, long? Sequence, IReadOnlyList<GraphStatus> Surfaces);
