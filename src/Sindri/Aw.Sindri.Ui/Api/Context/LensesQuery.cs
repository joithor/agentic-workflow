namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What QUERY /sindri/context/lenses is asked: sent as the JSON body.</summary>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="Depth">Depth, as served.</param>
/// <param name="Draft">Draft, as served.</param>
/// <param name="MapVersion">MapVersion, as served.</param>
internal sealed record LensesQuery(
    IReadOnlyList<string> Anchors,
    int? Depth,
    bool Draft,
    string? MapVersion);
