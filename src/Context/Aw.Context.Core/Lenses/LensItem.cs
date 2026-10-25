using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>One node of the own scope.</summary>
/// <param name="Node">The key.</param>
/// <param name="Label">The label as the caller reads it.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind it, or an accepted override (the Confirmation rule).</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
public sealed record LensItem(NodeKey Node, string Label, NodeKind Kind, Provenance Provenance, double Confidence, bool Confirmed, Results.Trust Trust)
{
    /// <summary>Schema 2 only: every anchor that reached it, in request order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Anchors { get; init; }

    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }

    /// <summary>Schema 2 only: the fewest edges or cascade levels from any anchor that reached it (0 for an anchor's own scope).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Depth { get; init; }
}
