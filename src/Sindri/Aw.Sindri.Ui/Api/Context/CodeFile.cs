namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A code file in the code band; LinkId names a suggested link.</summary>
/// <param name="Path">Path, as served.</param>
/// <param name="Implements">Implements, as served.</param>
/// <param name="Tests">Tests, as served.</param>
/// <param name="Provenance">Provenance, as served.</param>
/// <param name="Confidence">Confidence, as served.</param>
/// <param name="Confirmed">Confirmed, as served.</param>
/// <param name="Suggested">Suggested, as served.</param>
/// <param name="LinkId">LinkId, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Trust">Trust, as served.</param>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="Depth">Depth, as served.</param>
/// <param name="Repo">The repo it belongs to, or null (left out of the answer) when it belongs to no single repo.</param>
internal sealed record CodeFile(
    string Path,
    IReadOnlyList<string> Implements,
    IReadOnlyList<string> Tests,
    string Provenance,
    double Confidence,
    bool Confirmed,
    bool Suggested,
    string? LinkId,
    string Label,
    string Kind,
    string Trust,
    IReadOnlyList<string>? Anchors = null,
    int? Depth = null,
    string? Repo = null);
