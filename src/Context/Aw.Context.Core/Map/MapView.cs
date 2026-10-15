using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Map;

/// <summary>
/// What a caller may see of a stored snapshot (<see cref="MapAccess.View"/>): the stored graph and the overrides cut by one decision, then the overrides applied to the cut graph.
/// Applying <see cref="Overrides"/> to <see cref="Graph"/> (what the review queue and the planners do) gives <see cref="Applied"/> again. Only <see cref="MapAccess.View"/> builds one.
/// </summary>
public sealed class MapView
{
    internal MapView(GraphSnapshot graph, GraphSnapshot applied, IReadOnlyList<MapOverride> overrides, DenyCut? cut)
    {
        Graph = graph;
        Applied = applied;
        Overrides = overrides;
        Cut = cut;
        Filtered = cut?.Any ?? false;
    }

    /// <summary>The stored graph without what is hidden: what export, status counts and the review start from.</summary>
    public GraphSnapshot Graph { get; }

    /// <summary>The cut graph with the cut overrides applied: what the lenses and every walk read.</summary>
    public GraphSnapshot Applied { get; }

    /// <summary>The overrides that still mean something.</summary>
    public IReadOnlyList<MapOverride> Overrides { get; }

    /// <summary>True when a deny glob took or struck anything.</summary>
    public bool Filtered { get; }

    internal DenyCut? Cut { get; }

    internal MapView Scoped(GraphSnapshot graph, GraphSnapshot applied, IReadOnlyList<MapOverride> overrides) => new(graph, applied, overrides, Cut);

    /// <summary>The undo rows that take the decisions about nodes and links the deny globs take out with them (see <see cref="DenyCut.CutDecisions"/>), marked as the cut's.</summary>
    /// <param name="overrides">Every override.</param>
    /// <param name="at">When.</param>
    /// <returns>The rows, none when nothing is denied.</returns>
    public IReadOnlyList<MapOverride> UndoCutDecisions(IReadOnlyList<MapOverride> overrides, DateTimeOffset at) => Cut is null ? [] : Cut.UndoRows(overrides, at);

    /// <summary>The evidence a deny glob leaves of a scoping run's surface.</summary>
    /// <param name="evidence">The evidence, or null.</param>
    /// <returns>The same list when no glob applies.</returns>
    public IReadOnlyList<EvidenceRef>? AllowedEvidence(IReadOnlyList<EvidenceRef>? evidence) => Cut is null ? evidence : Cut.AllowedEvidence(evidence);
}
