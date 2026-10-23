namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One top-level feature of the map, as the overview shows it (a tile).</summary>
/// <param name="Key">The area's key (its feature's key; area:unassigned for what no feature claims).</param>
/// <param name="Label">The label, scrubbed; the person sees it as is.</param>
/// <param name="Trust">trusted or untrusted: the trust of the feature's own node.</param>
/// <param name="Members">The nodes whose primary area this is.</param>
/// <param name="ByKind">Members per kind.</param>
/// <param name="Unassigned">True for the area of what no feature claims.</param>
internal sealed record WholeArea(string Key, string Label, string Trust, int Members, IReadOnlyDictionary<string, int> ByKind, bool Unassigned);
