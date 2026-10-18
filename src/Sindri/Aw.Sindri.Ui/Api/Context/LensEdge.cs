namespace Aw.Sindri.Ui.Api.Context;

/// <summary>An edge between two nodes of the lenses; Link names it.</summary>
/// <param name="From">From, as served.</param>
/// <param name="To">To, as served.</param>
/// <param name="Type">Type, as served.</param>
/// <param name="Provenance">Provenance, as served.</param>
/// <param name="Link">Link, as served.</param>
/// <param name="Confirmed">Confirmed, as served.</param>
/// <param name="Confidence">Confidence, as served.</param>
internal sealed record LensEdge(
    string From,
    string To,
    string Type,
    string Provenance,
    string? Link,
    bool Confirmed,
    double Confidence);
