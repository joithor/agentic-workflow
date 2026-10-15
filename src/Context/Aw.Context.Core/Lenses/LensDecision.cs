using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>A decision that decides scope nodes.</summary>
/// <param name="Node">The decision's node key.</param>
/// <param name="Text">Its wording as the caller reads it.</param>
/// <param name="Decides">The scope nodes it decides.</param>
/// <param name="Supersedes">The decisions it supersedes.</param>
/// <param name="Quotes">The citation marker of the record it quotes; null while there are no citations (S-4).</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind it.</param>
/// <param name="Trust">Whether its wording may be read as instructions.</param>
/// <param name="At">When it was made, or null.</param>
/// <param name="Quote">The stored quote span and the record it came from, or null.</param>
public sealed record LensDecision(
    NodeKey Node,
    string Text,
    IReadOnlyList<NodeKey> Decides,
    IReadOnlyList<NodeKey> Supersedes,
    string? Quotes,
    Provenance Provenance,
    bool Confirmed,
    Results.Trust Trust,
    DateTimeOffset? At,
    LensQuote? Quote);
