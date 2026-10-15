using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>What a 'propose' override adds to a graph (S-4): an unconfirmed model node. It lives beside the applier, which builds the node, so the Overrides namespace needs nothing from Proposals.</summary>
public static class ProposalRows
{
    /// <summary>The confidence of a proposed node: a scoping run's guess, below a person's word.</summary>
    public const double Confidence = 0.5;

    /// <summary>The node a 'propose' override adds.</summary>
    /// <param name="o">The override.</param>
    /// <returns>An unconfirmed node (model provenance) carrying the proposal's evidence, or null when the override has no payload.</returns>
    public static GraphNode? NodeOf(MapOverride o)
    {
        ArgumentNullException.ThrowIfNull(o);
        throw new NotSupportedException("S-4: ProposalRows.NodeOf is not built yet");
    }
}
