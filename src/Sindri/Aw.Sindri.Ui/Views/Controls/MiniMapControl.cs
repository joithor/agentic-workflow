using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>One rectangle of the minimap: a card's place in the world and the brush it is filled with.</summary>
/// <param name="Rect">The card's rectangle, in world coordinates.</param>
/// <param name="Fill">The fill, from the card's state.</param>
/// <param name="Selected">Whether the card is the selected one (outlined).</param>
internal sealed record MiniMapNode(Rect Rect, IBrush Fill, bool Selected);

/// <summary>
/// The run canvas's overview (F3 spec 3 §2, "MiniMapControl"): one rounded rectangle per card on the whole world, the selected card
/// outlined, and the part of the world the stage shows as a rectangle. A press or a drag centres the stage on that point. It is not
/// focusable: the zoom pill and the F key are the keyboard's way, and it carries a name only.
/// </summary>
internal sealed class MiniMapControl : Control
{
    /// <summary>The cards.</summary>
    internal static readonly StyledProperty<IReadOnlyList<MiniMapNode>> NodesProperty =
        AvaloniaProperty.Register<MiniMapControl, IReadOnlyList<MiniMapNode>>(nameof(Nodes), []);

    /// <summary>The world's rectangle, in the same coordinates as the cards.</summary>
    internal static readonly StyledProperty<Rect> WorldProperty = AvaloniaProperty.Register<MiniMapControl, Rect>(nameof(World));

    /// <summary>The part of the world the stage shows.</summary>
    internal static readonly StyledProperty<Rect> ViewportProperty = AvaloniaProperty.Register<MiniMapControl, Rect>(nameof(Viewport));

    private const double Inset = 8;
    private const double CardRadius = 2;

    private bool _dragging;

    static MiniMapControl()
    {
        AffectsRender<MiniMapControl>(NodesProperty, WorldProperty, ViewportProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<MiniMapControl>(AccessibilityView.Control);
    }

    /// <summary>Initializes a new instance of the <see cref="MiniMapControl"/> class.</summary>
    public MiniMapControl()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>Raised when the pointer asks to centre the stage on a point of the world.</summary>
    internal event EventHandler<Point>? Centred;

    /// <summary>Gets or sets the cards.</summary>
    internal IReadOnlyList<MiniMapNode> Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    /// <summary>Gets or sets the world's rectangle.</summary>
    internal Rect World
    {
        get => GetValue(WorldProperty);
        set => SetValue(WorldProperty, value);
    }

    /// <summary>Gets or sets the part of the world the stage shows.</summary>
    internal Rect Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    // The scale that fits the world into the control with 8 px round it, and where the world's origin lands so it is centred.
    private (double Scale, double X, double Y) Frame()
    {
        var world = World;
        if (world.Width <= 0 || world.Height <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return (1, 0, 0);
        }

        var scale = Math.Min((Bounds.Width - (2 * Inset)) / world.Width, (Bounds.Height - (2 * Inset)) / world.Height);
        return (scale, ((Bounds.Width - (world.Width * scale)) / 2) - (world.X * scale), ((Bounds.Height - (world.Height * scale)) / 2) - (world.Y * scale));
    }

    private static Rect Map(Rect r, (double Scale, double X, double Y) f) => new((r.X * f.Scale) + f.X, (r.Y * f.Scale) + f.Y, r.Width * f.Scale, r.Height * f.Scale);

    /// <summary>The point of the world under a point of the control.</summary>
    /// <param name="point">A point in the control.</param>
    /// <returns>The same place in the world.</returns>
    internal Point ToWorld(Point point)
    {
        var f = Frame();
        return new Point((point.X - f.X) / f.Scale, (point.Y - f.Y) / f.Scale);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        var f = Frame();
        var outline = new Pen(Brush("Brush.Ink"), 1);
        foreach (var node in Nodes)
        {
            context.DrawRectangle(node.Fill, node.Selected ? outline : null, Map(node.Rect, f), CardRadius, CardRadius);
        }

        if (Viewport.Width > 0 && Viewport.Height > 0)
        {
            var ink = Brush("Brush.Ink");
            var tint = ink is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.06) : null;
            context.DrawRectangle(tint, new Pen(ink, 1.5), Map(Viewport, f));
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = true;
        e.Pointer.Capture(this);
        Centred?.Invoke(this, ToWorld(e.GetPosition(this)));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            Centred?.Invoke(this, ToWorld(e.GetPosition(this)));
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}
