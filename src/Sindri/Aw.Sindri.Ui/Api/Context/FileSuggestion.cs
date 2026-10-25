namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A suggested file-to-feature link.</summary>
/// <param name="LinkId">LinkId, as served.</param>
/// <param name="File">File, as served.</param>
/// <param name="Feature">Feature, as served.</param>
/// <param name="Confidence">Confidence, as served.</param>
/// <param name="Evidence">Evidence, as served.</param>
internal sealed record FileSuggestion(
    string LinkId,
    string File,
    string Feature,
    double Confidence,
    IReadOnlyList<string> Evidence);
