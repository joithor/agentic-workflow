namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One edge between two returned nodes.</summary>
/// <param name="From">The source node's key.</param>
/// <param name="To">The target node's key.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Link">The edge's own key.</param>
/// <param name="Provenance">Where it came from.</param>
/// <param name="Confidence">How sure the map is (0 to 1).</param>
/// <param name="Confirmed">True when a person confirmed it.</param>
internal sealed record WholeEdge(string From, string To, string Type, string Link, string Provenance, double Confidence, bool Confirmed);
