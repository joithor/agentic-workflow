namespace Aw.Context.Core.Lenses;

/// <summary>
/// What a lenses answer searched (scoping spec C6): only what nothing else in the answer says. Coverage, gaps and 'degraded' stay where they are.
/// </summary>
/// <param name="Anchors">The anchor keys that resolved, in request order.</param>
/// <param name="MapVersion">The map version read, as the answer's 'mapVersion' says it.</param>
/// <param name="Depth">How far the neighbors reached and the cascade ran.</param>
/// <param name="Hops">The cascade's hop kinds.</param>
/// <param name="Sources">Each source's state.</param>
/// <param name="Bands">Which bands came back empty.</param>
public sealed record LensSearched(IReadOnlyList<string> Anchors, string MapVersion, int Depth, IReadOnlyList<string> Hops, IReadOnlyList<SearchedSource> Sources, SearchedBands Bands);
