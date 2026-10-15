using Aw.Context.Core.Lenses;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The three lenses of one anchor: intent, product and code (GET /sindri/context/lenses?anchor=).</summary>
/// <param name="Anchor">A node key, name or alias.</param>
/// <param name="Draft">True to read the draft map.</param>
public sealed record GetContextLensesRequest(string Anchor, bool Draft = false) : IQuery<LensesView>;
