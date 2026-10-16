using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Linking;

/// <summary>A graph with the intent layer merged in, and the links whose target resolved to nothing (kept, counted, spec §6.1 "orphaned").</summary>
/// <param name="Graph">The merged graph.</param>
/// <param name="Orphaned">The links whose target is no node and no alias (a link to a record that is not synced yet waits instead).</param>
public sealed record IntentOverlayResult(GraphSnapshot Graph, IReadOnlyList<IntentLink> Orphaned);
