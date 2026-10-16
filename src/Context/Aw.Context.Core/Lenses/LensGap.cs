using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>A scope node missing a lens.</summary>
/// <param name="Node">The node.</param>
/// <param name="Missing">What is missing.</param>
/// <param name="Note">One line on what it does have, or null.</param>
public sealed record LensGap(NodeKey Node, IReadOnlyList<GapKind> Missing, string? Note);
