namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The part of GET /sindri/context/query's answer the Context box reads: the anchors a question resolved to, and the ones it suggests.</summary>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="SuggestedAnchors">SuggestedAnchors, as served.</param>
internal sealed record QueryAnchorsView(
    IReadOnlyList<string> Anchors,
    IReadOnlyList<string> SuggestedAnchors);
