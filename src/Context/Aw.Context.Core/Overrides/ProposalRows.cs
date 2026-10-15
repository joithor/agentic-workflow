using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

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
        return o.Proposal is { } p ? new GraphNode(o.Target, o.Label ?? o.Target.Name, Provenance.Model, Confidence, p.Evidence, At: o.At) : null;
    }
}
