using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Linking;

/// <summary>A checked pair of decisions: asked once per pair (a decision key is a hash of its record and quote, so a changed decision is a new pair).</summary>
/// <param name="A">The smaller decision key (ordinal).</param>
/// <param name="B">The larger decision key.</param>
/// <param name="Verdict">The model's verdict.</param>
/// <param name="At">When it was asked.</param>
public sealed record DecisionConflict(NodeKey A, NodeKey B, ConflictVerdict Verdict, DateTimeOffset At)
{
    /// <summary>Builds the pair with its keys in order.</summary>
    /// <param name="x">One decision key.</param>
    /// <param name="y">The other.</param>
    /// <param name="verdict">The verdict.</param>
    /// <param name="at">When it was asked.</param>
    /// <returns>The pair, smaller key first.</returns>
    public static DecisionConflict Of(NodeKey x, NodeKey y, ConflictVerdict verdict, DateTimeOffset at) =>
        string.CompareOrdinal(x.Value, y.Value) <= 0 ? new DecisionConflict(x, y, verdict, at) : new DecisionConflict(y, x, verdict, at);
}
