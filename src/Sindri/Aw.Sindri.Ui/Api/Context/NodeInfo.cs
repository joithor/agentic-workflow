namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The node a card shows.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Provenance">Provenance, as served.</param>
/// <param name="Confirmed">Confirmed, as served.</param>
/// <param name="Trust">Trust, as served.</param>
/// <param name="Confidence">Confidence, as served.</param>
/// <param name="Source">Where the node came from, or null.</param>
/// <param name="Description">A short plain-text summary, fenced when untrusted, or null.</param>
internal sealed record NodeInfo(
    string Key,
    string Kind,
    string Label,
    string Provenance,
    bool Confirmed,
    string Trust,
    double Confidence,
    NodeSource? Source = null,
    string? Description = null);
