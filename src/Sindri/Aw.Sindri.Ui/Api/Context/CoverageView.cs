namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Coverage of the answer; ByAnchor is served for a set.</summary>
/// <param name="Scope">Scope, as served.</param>
/// <param name="Intent">Intent, as served.</param>
/// <param name="Product">Product, as served.</param>
/// <param name="Code">Code, as served.</param>
/// <param name="Gaps">Gaps, as served.</param>
/// <param name="Map">Map, as served.</param>
/// <param name="ByAnchor">ByAnchor, as served.</param>
internal sealed record CoverageView(
    int Scope,
    CoverageIntent Intent,
    CoverageProduct Product,
    CoverageCode Code,
    IReadOnlyList<CoverageGap> Gaps,
    CoverageMap? Map = null,
    IReadOnlyDictionary<string, CoverageView>? ByAnchor = null);
