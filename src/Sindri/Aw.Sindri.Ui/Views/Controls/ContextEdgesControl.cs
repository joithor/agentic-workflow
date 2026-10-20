using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until slice 2 places the control in the context page

/// <summary>The layer that draws the context map's edges (F3 spec 1 §2, "Edges") in one Render pass.</summary>
internal sealed class ContextEdgesControl : Control, ICustomHitTest
{
    /// <summary>The edges to draw.</summary>
    internal static readonly StyledProperty<IReadOnlyList<MapEdge>> EdgesProperty =
        AvaloniaProperty.Register<ContextEdgesControl, IReadOnlyList<MapEdge>>(nameof(Edges), []);

    /// <summary>The keys of the edges drawn in WireHi.</summary>
    internal static readonly StyledProperty<IReadOnlySet<string>> HighlightedProperty =
        AvaloniaProperty.Register<ContextEdgesControl, IReadOnlySet<string>>(nameof(Highlighted), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>The keys of the edges drawn dimmed.</summary>
    internal static readonly StyledProperty<IReadOnlySet<string>> DimmedProperty =
        AvaloniaProperty.Register<ContextEdgesControl, IReadOnlySet<string>>(nameof(Dimmed), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>Gets or sets the edges to draw.</summary>
    internal IReadOnlyList<MapEdge> Edges
    {
        get => GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    /// <summary>Gets or sets the keys of the edges drawn in WireHi.</summary>
    internal IReadOnlySet<string> Highlighted
    {
        get => GetValue(HighlightedProperty);
        set => SetValue(HighlightedProperty, value);
    }

    /// <summary>Gets or sets the keys of the edges drawn dimmed.</summary>
    internal IReadOnlySet<string> Dimmed
    {
        get => GetValue(DimmedProperty);
        set => SetValue(DimmedProperty, value);
    }

    /// <summary>Gets or sets the key of the edge under the pointer.</summary>
    internal string? Hovered { get; set; }

    /// <summary>The key of the topmost edge whose hit stroke holds a point.</summary>
    /// <param name="point">A point in this control.</param>
    /// <returns>The edge's key, or null.</returns>
    internal string? EdgeAt(Point point) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool HitTest(Point point) => false;
}

#pragma warning restore MA0182
