namespace Aw.Context.Core.Graph;

/// <summary>A node as 'context show' answers it: the <see cref="NodeFact"/> fields, where the node came from and a short description.</summary>
/// <param name="Key">The key.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Label">The label, scrubbed, one line, at most 200 characters; inside the untrusted fence when <paramref name="Trust"/> is untrusted.</param>
/// <param name="Provenance">Who produced it (after overrides: an accepted node is human).</param>
/// <param name="Confirmed">True when a human confirmed it.</param>
/// <param name="Trust">Whether its text can be trusted.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Source">The record or file it came from, or null when the map stores none.</param>
/// <param name="Description">A one-line summary of the stored text (a decision's quote), at most 280 characters cut on a word, fenced as untrusted like a quote; null when the map stores none.</param>
public sealed record NodeDetail(
    NodeKey Key,
    NodeKind Kind,
    string Label,
    Results.Provenance Provenance,
    bool Confirmed,
    Results.Trust Trust,
    double Confidence,
    NodeSource? Source = null,
    string? Description = null)
{
    /// <summary>The detail for <paramref name="fact"/>.</summary>
    /// <param name="fact">The node fact.</param>
    /// <param name="source">Where it came from, or null.</param>
    /// <param name="description">Its description, or null.</param>
    /// <returns>The detail.</returns>
    public static NodeDetail Of(NodeFact fact, NodeSource? source = null, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return new NodeDetail(fact.Key, fact.Kind, fact.Label, fact.Provenance, fact.Confirmed, fact.Trust, fact.Confidence, source, description);
    }

    /// <summary>The plain fact (what the text form prints).</summary>
    /// <returns>The fact.</returns>
    public NodeFact ToFact() => new(Key, Kind, Label, Provenance, Confirmed, Trust, Confidence);
}
