using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>An edge among found nodes and the anchors, as the lens returned it.</summary>
/// <param name="From">One end (a node key or an anchor).</param>
/// <param name="To">The other end.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Provenance">Where the edge came from.</param>
/// <param name="Confirmed">True when the owner or a review confirmed it.</param>
/// <param name="Trust">Its trust label.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensEdge(string From, string To, string Type, string Provenance, bool Confirmed, LensTrust Trust);
