using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>
/// The whole map's world (whole-map spec §3.2), drawn in one <see cref="Render"/> pass: every edge and node (or every tile) of the answer, and no control per
/// node. Below the dot zoom nodes are dots in their area's colour with one label per area; above it, chips with their words, only for what is on screen.
/// Lives in a <see cref="Controls.ZoomBorder"/>, which moves and scales it; the host tells it the zoom and the part of the world on screen.
/// </summary>
internal sealed partial class WholeMapCanvas : Control
{
    /// <summary>The map drawn.</summary>
    internal static readonly StyledProperty<WholeMapViewModel?> MapProperty = AvaloniaProperty.Register<WholeMapCanvas, WholeMapViewModel?>(nameof(Map));

    /// <summary>The zoom scale, which decides dots or chips.</summary>
    internal static readonly StyledProperty<double> ScaleProperty = AvaloniaProperty.Register<WholeMapCanvas, double>(nameof(Scale), 1);

    /// <summary>The part of the world on screen, so only that is drawn.</summary>
    internal static readonly StyledProperty<Rect> ViewportProperty = AvaloniaProperty.Register<WholeMapCanvas, Rect>(nameof(Viewport));

    private const double ClickSlop = 3;
    private Point? _pressed;

    static WholeMapCanvas()
    {
        AffectsRender<WholeMapCanvas>(MapProperty, ScaleProperty, ViewportProperty);
        AffectsMeasure<WholeMapCanvas>(MapProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WholeMapCanvas>(AccessibilityView.Raw);   // the dots have no nodes of their own: the list of features is the accessible map
    }

    /// <summary>Gets or sets the map drawn.</summary>
    internal WholeMapViewModel? Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    /// <summary>Gets or sets the zoom scale.</summary>
    internal double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Gets or sets the part of the world on screen.</summary>
    internal Rect Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => Map?.Geometry is { } geometry ? new Size(geometry.World.Width, geometry.World.Height) : default;

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressed = e.GetPosition(this);
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var at = e.GetPosition(this);
        if (_pressed is { } start && Math.Abs(at.X - start.X) <= ClickSlop && Math.Abs(at.Y - start.Y) <= ClickSlop)
        {
            Click(at);
        }

        _pressed = null;
    }

    // A tile opens its feature; a node (above the dot zoom or as a dot) is chosen for the inspector.
    private void Click(Point at)
    {
        if (Map is not { Geometry: { } geometry } map)
        {
            return;
        }

        if (geometry.Tiles.FirstOrDefault(t => t.Rect.Contains(at.X, at.Y)) is { } tile)
        {
            map.OpenArea(tile.Key);
        }
        else if (geometry.Nodes.FirstOrDefault(n => n.Rect.Contains(at.X, at.Y)) is { } node)
        {
            map.ChooseNode(node.Key);
        }
    }
}
