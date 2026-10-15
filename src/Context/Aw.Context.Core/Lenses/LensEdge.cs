using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>An edge between items of the bands (a record that mentions a feature, a file that implements one, two product items).</summary>
/// <param name="From">The source node.</param>
/// <param name="To">The target node.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Link">The edge's id.</param>
/// <param name="Confirmed">False for a model-suggested edge nobody accepted.</param>
/// <param name="Confidence">0 to 1.</param>
public sealed record LensEdge(NodeKey From, NodeKey To, EdgeType Type, Provenance Provenance, LinkId Link, bool Confirmed, double Confidence);
