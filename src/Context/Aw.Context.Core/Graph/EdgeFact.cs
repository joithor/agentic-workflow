using Aw.Context.Core.Results;

namespace Aw.Context.Core.Graph;

/// <summary>An edge as an answer shows it.</summary>
/// <param name="From">The source node.</param>
/// <param name="Type">The type.</param>
/// <param name="To">The target node.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confirmed">True when a human confirmed it.</param>
/// <param name="Trust">Whether it can be trusted.</param>
/// <param name="Order">Its position in a flow ('step-of'), or null.</param>
public sealed record EdgeFact(NodeKey From, EdgeType Type, NodeKey To, Provenance Provenance, bool Confirmed, Results.Trust Trust, int? Order = null)
{
    /// <summary>The fact for <paramref name="edge"/>: confirmed when human, trust from <see cref="TrustPolicy"/>.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>The fact.</returns>
    public static EdgeFact Of(GraphEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        var confirmed = edge.Provenance == Provenance.Human;
        return new EdgeFact(edge.From, edge.Type, edge.To, edge.Provenance, confirmed, TrustPolicy.For(edge.Provenance, confirmed), edge.Order);
    }
}
