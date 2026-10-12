using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>A graph with its overrides applied.</summary>
/// <param name="Graph">The result.</param>
/// <param name="Orphaned">Overrides whose target no longer resolves: kept, and queued for review (spec §8.7).</param>
/// <param name="Inapplicable">Overrides whose target resolves but that can't apply: an empty label, a move or add under a node that isn't a feature, a merge into itself, into a missing node or across kinds. Kept, never applied.</param>
public sealed record AppliedMap(GraphSnapshot Graph, IReadOnlyList<MapOverride> Orphaned, IReadOnlyList<MapOverride> Inapplicable);
