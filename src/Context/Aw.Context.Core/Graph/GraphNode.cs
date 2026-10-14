using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Graph;

/// <summary>A product-graph node (spec §6.1). Its trust is never stored or read from a file: <see cref="TrustPolicy"/> computes it.</summary>
/// <param name="Key">The stable key; its scheme is the kind.</param>
/// <param name="Label">The display name.</param>
/// <param name="Provenance">Who produced it (an authored map's default is human).</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Evidence">The records it rests on, or null for none.</param>
/// <param name="Attributes">Kind-specific attributes (preconditions, states), or null.</param>
/// <param name="At">When it was written; an import keeps the file's value.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphNode(
    NodeKey Key,
    string Label,
    Provenance Provenance = Provenance.Human,
    double Confidence = 1.0,
    IReadOnlyList<EvidenceRef>? Evidence = null,
    JsonElement? Attributes = null,
    DateTimeOffset? At = null)
{
    /// <summary>The evidence, never null.</summary>
    [JsonIgnore]
    public IReadOnlyList<EvidenceRef> EvidenceOrEmpty => Evidence ?? [];
}
