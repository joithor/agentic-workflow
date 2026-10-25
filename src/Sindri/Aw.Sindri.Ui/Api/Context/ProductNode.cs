namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A product node in a band.</summary>
/// <param name="Node">Node, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Provenance">Provenance, as served.</param>
/// <param name="Confidence">Confidence, as served.</param>
/// <param name="Confirmed">Confirmed, as served.</param>
/// <param name="Trust">Trust, as served.</param>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="Depth">Depth, as served.</param>
/// <param name="Path">Path, as served.</param>
/// <param name="Repo">The repo it belongs to, or null (left out of the answer) when it belongs to no single repo.</param>
internal sealed record ProductNode(
    string Node,
    string Label,
    string Kind,
    string Provenance,
    double Confidence,
    bool Confirmed,
    string Trust,
    IReadOnlyList<string>? Anchors = null,
    int? Depth = null,
    IReadOnlyList<string>? Path = null,
    string? Repo = null);
