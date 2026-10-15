using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Map;

/// <summary>
/// What a caller may see of a stored snapshot (<see cref="MapAccess.View"/>): the stored graph and the overrides cut by one decision, then the overrides applied to the cut graph.
/// Applying <see cref="Overrides"/> to <see cref="Graph"/> (what the review queue and the planners do) gives <see cref="Applied"/> again.
/// </summary>
/// <param name="Graph">The stored graph without what is hidden: what export, status counts and the review start from.</param>
/// <param name="Applied">The cut graph with the cut overrides applied: what the lenses and every walk read.</param>
/// <param name="Overrides">The overrides that still mean something.</param>
/// <param name="Filtered">True when a deny glob took or struck anything.</param>
public sealed record MapView(GraphSnapshot Graph, GraphSnapshot Applied, IReadOnlyList<MapOverride> Overrides, bool Filtered)
{
    internal DenyCut? Cut { get; init; }

    /// <summary>The active decisions about nodes and links the deny globs take out (see <see cref="DenyCut.CutDecisions"/>).</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The decisions, none when nothing is denied.</returns>
    public IReadOnlyList<MapOverride> CutDecisions(IReadOnlyList<MapOverride> overrides) => Cut is null ? [] : Cut.CutDecisions(overrides);

    /// <summary>The evidence a deny glob leaves of a scoping run's surface.</summary>
    /// <param name="evidence">The evidence, or null.</param>
    /// <returns>The same list when no glob applies.</returns>
    public IReadOnlyList<EvidenceRef>? AllowedEvidence(IReadOnlyList<EvidenceRef>? evidence) => Cut is null ? evidence : Cut.AllowedEvidence(evidence);
}
