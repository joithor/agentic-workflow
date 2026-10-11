using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Lint;

/// <summary>A plain capture of the page for the lint rules (no live browser needed).</summary>
/// <param name="Elements">The captured elements, in DOM order.</param>
/// <param name="Viewport">The viewport.</param>
public sealed record PageSnapshot(IReadOnlyList<PageElement> Elements, ViewportSize Viewport);
