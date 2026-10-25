using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>A node outside the own scope that the anchor's neighborhood or cascade reaches.</summary>
/// <param name="Node">The key.</param>
/// <param name="Label">The label as the caller reads it.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind it, or an accepted override.</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
/// <param name="Depth">Edges from the anchor (neighbors) or cascade levels (affects).</param>
/// <param name="Path">The hops, each '&lt;edge type&gt; &lt;node key&gt;' (neighbors) or the cascade's own words (affects).</param>
public sealed record LensReach(
    NodeKey Node,
    string Label,
    NodeKind Kind,
    Provenance Provenance,
    double Confidence,
    bool Confirmed,
    Results.Trust Trust,
    int Depth,
    IReadOnlyList<string> Path)
{
    /// <summary>Schema 2 only: every anchor that reached it, in request order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Anchors { get; init; }

    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }
}
