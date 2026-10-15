using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Proposals;

/// <summary>How a scoping run's surface becomes a node (S-4): its key, and the unconfirmed model node a 'propose' override adds.</summary>
public static class ProposalNodes
{
    /// <summary>The confidence of a proposed node: a scoping run's guess, below a person's word.</summary>
    public const double Confidence = 0.5;

    /// <summary>The key a surface would have as a node.</summary>
    /// <param name="surface">The surface.</param>
    /// <returns>Its key, or null when the run gave too little: no key and no kind, a surface without its key, or a kind a proposal can't have.</returns>
    public static NodeKey? KeyOf(ProposedSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        throw new NotSupportedException("S-4: ProposalNodes.KeyOf is not built yet");
    }

    /// <summary>The node a 'propose' override adds.</summary>
    /// <param name="o">The override.</param>
    /// <returns>An unconfirmed node (model provenance) carrying the proposal's evidence, or null when the override has no payload.</returns>
    public static GraphNode? NodeOf(MapOverride o)
    {
        ArgumentNullException.ThrowIfNull(o);
        throw new NotSupportedException("S-4: ProposalNodes.NodeOf is not built yet");
    }

    /// <summary>The node a surface would become, to compare its <see cref="RejectionIdentity"/> with an earlier rejection.</summary>
    /// <param name="key">The surface's key.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>The candidate node.</returns>
    public static GraphNode Candidate(NodeKey key, ProposedSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _ = key;
        throw new NotSupportedException("S-4: ProposalNodes.Candidate is not built yet");
    }
}
