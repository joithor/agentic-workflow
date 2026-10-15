using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>An edge between items of different bands (a record that mentions a feature, a file that implements one).</summary>
/// <param name="From">The source node.</param>
/// <param name="To">The target node.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Link">The edge's id.</param>
/// <param name="Confirmed">False for a model-suggested edge nobody accepted.</param>
/// <param name="Confidence">0 to 1.</param>
public sealed record LensEdge(NodeKey From, NodeKey To, EdgeType Type, LinkId Link, bool Confirmed, double Confidence);
