namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/lenses is asked: each anchor travels as a repeated query value.</summary>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="Depth">Depth, as served.</param>
/// <param name="Draft">Draft, as served.</param>
/// <param name="MapVersion">MapVersion, as served.</param>
internal sealed record LensesQuery(
    IReadOnlyList<string> Anchors,
    int? Depth,
    bool Draft,
    string? MapVersion);
