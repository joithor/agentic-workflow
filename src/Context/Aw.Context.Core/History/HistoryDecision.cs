using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.History;

/// <summary>A lens decision with the anchors that reached it and the 'before' label.</summary>
/// <param name="Node">The decision's node key.</param>
/// <param name="Text">Its wording.</param>
/// <param name="Decides">The scope nodes it decides.</param>
/// <param name="Supersedes">The decisions it supersedes (only those still listed).</param>
/// <param name="Quotes">The citation marker of the record it quotes, or null.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind it.</param>
/// <param name="Trust">Whether its wording may be read as instructions.</param>
/// <param name="At">When it was made, or null.</param>
/// <param name="Quote">The stored quote span and its record, or null.</param>
/// <param name="Anchors">The anchors whose scope reached it.</param>
/// <param name="Leaky">True when a 'before' cut can't vouch that the decision's record is as it was then.</param>
public sealed record HistoryDecision(
    NodeKey Node,
    string Text,
    IReadOnlyList<NodeKey> Decides,
    IReadOnlyList<NodeKey> Supersedes,
    string? Quotes,
    Provenance Provenance,
    bool Confirmed,
    Results.Trust Trust,
    DateTimeOffset? At,
    LensQuote? Quote,
    IReadOnlyList<NodeKey> Anchors,
    bool Leaky)
{
    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }
}
