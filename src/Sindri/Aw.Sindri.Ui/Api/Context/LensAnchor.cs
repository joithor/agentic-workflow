namespace Aw.Sindri.Ui.Api.Context;

/// <summary>An anchor the lenses answered for.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Trust">Trust, as served.</param>
internal sealed record LensAnchor(
    string Key,
    string Label,
    string Kind,
    string Trust);
