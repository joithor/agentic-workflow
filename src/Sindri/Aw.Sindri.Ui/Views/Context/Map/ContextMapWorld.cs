using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Context.Map;

/// <summary>
/// The context map's world (F3 spec 1 §2): one canvas, 1360 px wide, holding the band frames, the edges layer, the band titles
/// (on the page colour, so an edge passes behind them), the nodes and the label pills, in that order from the bottom. The
/// <see cref="ZoomBorder"/> that holds it moves and scales it; this control only places what the map view model holds.
/// </summary>
internal sealed class ContextMapWorld : Canvas
{
    /// <summary>The map to draw; null draws nothing.</summary>
    internal static readonly StyledProperty<ContextMapViewModel?> MapProperty = AvaloniaProperty.Register<ContextMapWorld, ContextMapViewModel?>(nameof(Map));


    static ContextMapWorld()
    {
        _ = MapProperty.Changed.AddClassHandler<ContextMapWorld>((w, _) => w.Rebuild());
    }

    /// <summary>Gets the edges layer (for hover and the highlight).</summary>
    internal GraphEdgesControl? EdgesLayer { get; private set; }

    /// <summary>Gets the node views by node key.</summary>
    internal IReadOnlyDictionary<string, MapNodeView> NodeViews { get; private set; } = new Dictionary<string, MapNodeView>(StringComparer.Ordinal);

    /// <summary>Gets the pills by edge key.</summary>
    internal IReadOnlyDictionary<string, Control> PillViews { get; private set; } = new Dictionary<string, Control>(StringComparer.Ordinal);

    /// <summary>Gets or sets the map to draw.</summary>
    internal ContextMapViewModel? Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();   // the brushes and radii it reads come from the tree it is now in
    }

    private void Rebuild()
    {
        Children.Clear();
        EdgesLayer = null;
        NodeViews = new Dictionary<string, MapNodeView>(StringComparer.Ordinal);
        PillViews = new Dictionary<string, Control>(StringComparer.Ordinal);
        if (Map is not { } map)
        {
            return;
        }

        Width = map.WorldWidth;
        Height = map.WorldHeight;
        foreach (var band in map.Bands)
        {
            Place(Frame(band), 0, band.Top);
        }

        EdgesLayer = new GraphEdgesControl { Width = map.WorldWidth, Height = map.WorldHeight, Edges = map.Edges, IsHitTestVisible = false };
        Place(EdgesLayer, 0, 0);
        foreach (var band in map.Bands)
        {
            Place(Title(band), 10, band.Top + 10);
            Place(Meta(band), 0, band.Top + 10);
        }

        NodeViews = PlaceAll(map.Nodes, n => n.Key, n => (n.X, n.Y), n => new MapNodeView { DataContext = n });
        PillViews = PlaceAll<MapPillViewModel, Control>(map.Pills, p => p.Key, p => (p.X, p.Y), Pill);
    }

    // Builds a view for each item, places it at its point and returns the views by key.
    private Dictionary<string, TView> PlaceAll<TItem, TView>(IEnumerable<TItem> items, Func<TItem, string> key, Func<TItem, (double X, double Y)> at, Func<TItem, TView> build)
        where TView : Control
    {
        var views = new Dictionary<string, TView>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var view = build(item);
            views[key(item)] = view;
            var (x, y) = at(item);
            Place(view, x, y);
        }

        return views;
    }

    private void Place(Control control, double x, double y)
    {
        SetLeft(control, x);
        SetTop(control, y);
        Children.Add(control);
    }

    private Border Frame(MapBandViewModel band)
    {
        var frame = new Border
        {
            Width = band.Width,
            Height = band.Height,
            Background = Brush("Brush.LaneFill"),
            BorderBrush = Brush("Brush.Hairline"),
            BorderThickness = new Thickness(1),
            CornerRadius = Resource("Radius.MD", new CornerRadius(8)),
            IsHitTestVisible = false,
        };
        AutomationProperties.SetAccessibilityView(frame, AccessibilityView.Raw);
        return frame;
    }

    // The band's name, on the page colour so an edge that passes behind it does not strike it through.
    private Border Title(MapBandViewModel band)
    {
        var text = new TextBlock { Text = band.Title, FontSize = 15, LineHeight = 22, FontWeight = FontWeight.SemiBold, Foreground = Brush("Brush.Ink") };
        var title = new Border { Background = Brush("Brush.Canvas"), Padding = new Thickness(6, 0), Child = text, IsHitTestVisible = false };
        AutomationProperties.SetName(title, band.Name);
        AutomationProperties.SetAccessibilityView(title, AccessibilityView.Control);
        AutomationProperties.SetControlTypeOverride(title, Avalonia.Automation.Peers.AutomationControlType.Text);
        return title;
    }

    private Border Meta(MapBandViewModel band)
    {
        var text = new TextBlock { Text = band.Meta, FontSize = 12, LineHeight = 16, Foreground = Brush("Brush.InkSubtle"), TextAlignment = TextAlignment.Right };
        var meta = new Border { Width = band.Width - 20, Child = text, IsHitTestVisible = false };
        AutomationProperties.SetAccessibilityView(meta, AccessibilityView.Raw);
        return meta;
    }

    // A pill is centred on its point: a zero-size host the pill overflows evenly on both sides.
    private Grid Pill(MapPillViewModel pill)
    {
        var text = new TextBlock { Text = pill.Text, FontSize = 11, LineHeight = 16, Foreground = Brush("Brush.InkMuted"), TextWrapping = TextWrapping.NoWrap };
        var box = new Border
        {
            Background = Brush("Brush.Surface3"),
            BorderBrush = Brush("Brush.Hairline"),
            BorderThickness = new Thickness(1),
            CornerRadius = Resource("Radius.SM", new CornerRadius(6)),
            Padding = new Thickness(7, 1),
            Child = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        if (pill.Dashed)
        {
            box.BorderBrush = Brush("Brush.Wire");
            box.BorderThickness = new Thickness(1);
        }

        box.Opacity = pill.Always ? 1 : 0;
        AutomationProperties.SetName(box, pill.Text);
        AutomationProperties.SetAccessibilityView(box, pill.Always ? AccessibilityView.Control : AccessibilityView.Raw);   // a hover-only label is invisible until hover exists
        AutomationProperties.SetControlTypeOverride(box, Avalonia.Automation.Peers.AutomationControlType.Text);
        return new Grid { Width = 0, Height = 0, Children = { box }, IsHitTestVisible = false };
    }

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    private T Resource<T>(string key, T fallback) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is T found ? found : fallback;
}
