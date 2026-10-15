using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Proposals;

/// <summary>What <see cref="ProposalPlanner"/> decided for one proposal.</summary>
/// <param name="Rows">The 'propose' rows to write.</param>
/// <param name="Existing">Surfaces already in the map, already proposed, or repeated in the request.</param>
/// <param name="Rejected">Surfaces a person rejected earlier.</param>
/// <param name="Unmapped">Surfaces the run gave too little for: no key and no usable kind.</param>
public sealed record ProposalPlan(IReadOnlyList<MapOverride> Rows, int Existing, int Rejected, int Unmapped);
