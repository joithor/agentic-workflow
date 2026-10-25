namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What the answer searched.</summary>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="MapVersion">MapVersion, as served.</param>
/// <param name="Depth">Depth, as served.</param>
/// <param name="Hops">Hops, as served.</param>
/// <param name="Sources">Sources, as served.</param>
/// <param name="Bands">Bands, as served.</param>
internal sealed record SearchedView(
    IReadOnlyList<string> Anchors,
    string MapVersion,
    int Depth,
    IReadOnlyList<string> Hops,
    IReadOnlyList<SearchedSource> Sources,
    SearchedBands Bands);
