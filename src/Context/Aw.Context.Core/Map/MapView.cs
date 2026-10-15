using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Map;

/// <summary>What a caller may see of a stored snapshot (<see cref="MapAccess.View"/>): the same cut applied to every form of it.</summary>
/// <param name="Graph">The stored graph without what is hidden: what export, status counts and the review start from.</param>
/// <param name="Applied">The graph with the overrides applied, without what is hidden: what the lenses and every walk read.</param>
/// <param name="Overrides">The overrides that still mean something.</param>
/// <param name="Filtered">True when a deny glob took or struck anything.</param>
public sealed record MapView(GraphSnapshot Graph, GraphSnapshot Applied, IReadOnlyList<MapOverride> Overrides, bool Filtered);
