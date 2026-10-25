using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>One lens's frozen graph part (scoping spec §8.3): what it searched from, how far, and what it found.</summary>
/// <param name="Anchors">The anchors the lens searched from (map node keys or 'code:&lt;path&gt;' file keys).</param>
/// <param name="Depth">How many edges out the lens followed.</param>
/// <param name="Hops">The hop kinds the lens walked, in the order it walked them (empty at depth 0); no order is required.</param>
/// <param name="MapVersion">The map version the lens read, as the context engine writes it (for example 'v12@a41b07d'); not empty.</param>
/// <param name="FoundCount">The full count of what the lens found; more than <paramref name="Nodes"/> and <paramref name="Items"/> hold when a band was truncated.</param>
/// <param name="Nodes">The found nodes.</param>
/// <param name="Edges">The edges among the found nodes and the anchors.</param>
/// <param name="Items">The found items with no graph key.</param>
/// <param name="Truncated">The bands the context engine cut at its 100-item cap.</param>
/// <param name="EdgesOutOfScope">How many edges the lens left out because an end was outside its found nodes and anchors; 0 (and absent from the file) when none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensEntry(
    IReadOnlyList<string> Anchors,
    int Depth,
    IReadOnlyList<LensHop> Hops,
    string MapVersion,
    int FoundCount,
    IReadOnlyList<LensNode> Nodes,
    IReadOnlyList<LensEdge> Edges,
    IReadOnlyList<LensFound> Items,
    IReadOnlyList<LensBand> Truncated,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int EdgesOutOfScope = 0);
