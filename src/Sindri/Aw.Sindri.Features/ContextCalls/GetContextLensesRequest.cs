using Aw.Context.Core.Lenses;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The three lenses of one anchor: intent, product and code (GET /sindri/context/lenses?anchor=).</summary>
/// <param name="Anchor">A node key, name or alias.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="Depth">How far the neighbors reach and the cascade runs (0 to 4), or null for the profile's 'context.cascadeDepth'.</param>
public sealed record GetContextLensesRequest(string Anchor, bool Draft = false, int? Depth = null) : IQuery<LensesView>;
