namespace Aw.Sindri.Ui.Api.Context;

/// <summary>An edge into or out of a node.</summary>
/// <param name="From">From, as served.</param>
/// <param name="Type">Type, as served.</param>
/// <param name="To">To, as served.</param>
/// <param name="Provenance">Provenance, as served.</param>
/// <param name="Confirmed">Confirmed, as served.</param>
/// <param name="Trust">Trust, as served.</param>
/// <param name="Order">Order, as served.</param>
/// <param name="OtherKind">The kind of the node at the other end, when served.</param>
/// <param name="OtherLabel">The label of the node at the other end, when served.</param>
internal sealed record NodeEdge(
    string From,
    string Type,
    string To,
    string Provenance,
    bool Confirmed,
    string? Trust = null,
    int? Order = null,
    string? OtherKind = null,
    string? OtherLabel = null);
