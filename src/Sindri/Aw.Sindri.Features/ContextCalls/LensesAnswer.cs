using Aw.Context.Core.Lenses;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The answer to a lenses call: the single-anchor view (schema 1) or the set view (schema 2), exactly one of them.</summary>
/// <param name="One">The view of one distinct anchor, or null for a set.</param>
/// <param name="Many">The view of several distinct anchors, or null for one.</param>
public sealed record LensesAnswer(LensesView? One, LensesSetView? Many);
