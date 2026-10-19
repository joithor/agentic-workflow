namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A feature a file or directory implements.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Provenance">Provenance, as served.</param>
/// <param name="Confidence">Confidence, as served.</param>
/// <param name="Confirmed">Confirmed, as served.</param>
/// <param name="LinkId">LinkId, as served.</param>
/// <param name="Edge">Edge, as served.</param>
internal sealed record FileFeature(
    string Key,
    string Provenance,
    double? Confidence,
    bool Confirmed,
    string? LinkId,
    string Edge);
