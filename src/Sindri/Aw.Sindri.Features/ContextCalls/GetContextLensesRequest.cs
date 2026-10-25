using Aw.Context.Core.Lenses;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The three lenses of one anchor: intent, product and code (GET /sindri/context/lenses?anchor=).</summary>
/// <param name="Anchor">A node key, name or alias.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="Depth">How far the neighbors reach and the cascade runs (0 to 4), or null for the profile's 'context.cascadeDepth'.</param>
/// <param name="MapVersion">A retained map version ('v3', '3' or 'v3@a41b07d'), or null for the current one; not valid with <paramref name="Draft"/>.</param>
/// <param name="Hops">A comma list of state, flow, dependency and component for the cascade, or null for the first three (anything else is SND-CONTEXT-140).</param>
public sealed record GetContextLensesRequest(string Anchor, bool Draft = false, int? Depth = null, string? MapVersion = null, string? Hops = null) : IQuery<LensesView>;
