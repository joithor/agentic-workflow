using Aw.Context.Core.Lenses;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The union of the lenses of several anchors (GET /sindri/context/lenses?anchor=&amp;anchor=; schema 2).</summary>
/// <param name="Anchors">One to 20 node keys, names or aliases.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="Depth">How far the neighbors reach and the cascade runs (0 to 4), or null for the profile's 'context.cascadeDepth'.</param>
/// <param name="MapVersion">A retained map version, or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record GetContextLensesSetRequest(IReadOnlyList<string> Anchors, bool Draft = false, int? Depth = null, string? MapVersion = null) : IQuery<LensesSetView>;
