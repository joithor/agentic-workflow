using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.History;

/// <summary>A lens record with the instants the cut reads. Its text is never fetched here.</summary>
/// <param name="Key">The record's node key.</param>
/// <param name="Label">Its scrubbed label.</param>
/// <param name="Source">Where it lives.</param>
/// <param name="Mentions">The scope nodes it mentions.</param>
/// <param name="Trust">Record text is untrusted.</param>
/// <param name="State">The citation's state.</param>
/// <param name="Cites">The citation markers.</param>
/// <param name="Anchors">The anchors whose scope reached it.</param>
/// <param name="CreatedAt">When the source created it, or null (a row from before the column, or a source that gives none).</param>
/// <param name="UpdatedAt">When the source last changed it, or null when its version isn't a timestamp.</param>
/// <param name="FetchedAt">When the sync read it.</param>
/// <param name="Leaky">True when a 'before' cut can't vouch that the record is as it was then.</param>
public sealed record HistoryRecord(
    NodeKey Key,
    string Label,
    string Source,
    IReadOnlyList<NodeKey> Mentions,
    Results.Trust Trust,
    CitationState State,
    IReadOnlyList<string> Cites,
    IReadOnlyList<NodeKey> Anchors,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? FetchedAt,
    bool Leaky)
{
    /// <summary>Its kind (always a record).</summary>
    public NodeKind Kind { get; } = NodeKind.Record;

    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }
}
