using Aw.Context.Core.Results;

namespace Aw.Context.Core.Graph;

/// <summary>Trust follows provenance (spec §7, D15): deterministic and human facts are trusted; model output only once a human accepts it.</summary>
public static class TrustPolicy
{
    /// <summary>The trust of a fact.</summary>
    /// <param name="provenance">Who produced it.</param>
    /// <param name="accepted">Whether a human accepted it (an 'accept' override).</param>
    /// <returns>Its trust.</returns>
    public static Results.Trust For(Provenance provenance, bool accepted) =>
        provenance == Provenance.Model && !accepted ? Results.Trust.Untrusted : Results.Trust.Trusted;

    /// <summary>The trust of a node: a record's title is someone else's text, so it is untrusted whoever imported it; any other node follows <see cref="For"/>.</summary>
    /// <param name="node">The node.</param>
    /// <param name="accepted">Whether a human accepted it.</param>
    /// <returns>Its trust.</returns>
    public static Results.Trust ForNode(GraphNode node, bool accepted)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Key.Kind == NodeKind.Record ? Results.Trust.Untrusted : For(node.Provenance, accepted);
    }
}
