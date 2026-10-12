using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>A graph with its overrides applied.</summary>
/// <param name="Graph">The result.</param>
/// <param name="Orphaned">Overrides whose target (or other node) no longer resolves: kept, and queued for review (spec §8.7).</param>
public sealed record AppliedMap(GraphSnapshot Graph, IReadOnlyList<MapOverride> Orphaned);
