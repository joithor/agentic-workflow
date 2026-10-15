using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Review;

/// <summary>The map a review decision is planned against, already cut to what the caller may see (<see cref="Map.MapAccess.View"/>).</summary>
/// <param name="Graph">The stored graph, before overrides.</param>
/// <param name="Overrides">Every visible override.</param>
/// <param name="IsDraft">True for the latest draft; false for the current map, which a suggested file link or an undo may be decided against when there is no draft (so refusals point at the current map, not at a queue that answers 044).</param>
public sealed record ReviewBasis(GraphSnapshot Graph, IReadOnlyList<MapOverride> Overrides, bool IsDraft = true);
