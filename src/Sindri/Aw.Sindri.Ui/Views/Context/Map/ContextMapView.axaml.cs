using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Context.Map;

/// <summary>The context map's stage: the canvas and the cards over it (F3 spec 1). Bound to a <see cref="ContextMapViewModel"/>.</summary>
internal sealed partial class ContextMapView : UserControl
{
    private const double NarrowStage = 1100;   // under this the summary card starts collapsed
    private bool _fit = true;
    private bool _sized;

    /// <summary>Initializes a new instance of the <see cref="ContextMapView"/> class.</summary>
    public ContextMapView()
    {
        InitializeComponent();
        HudTitle.Text = Labels.ContextMapWhyTitle;
        HudSub.Text = Labels.ContextMapWhySub;
        HudNone.Text = Labels.ContextMapWhyNone;
        LegendConfirmed.Text = Labels.ContextMapLegendConfirmed;
        LegendSuggested.Text = Labels.ContextMapLegendSuggested;
        SetName(ZoomOutButton, Labels.ContextMapZoomOut);
        SetName(ZoomInButton, Labels.ContextMapZoomIn);
        SetName(ZoomFitButton, Labels.ContextMapZoomFit);
        AutomationProperties.SetName(ContextZoom, Labels.ContextMapZoomPct(1));
        SetHud(collapsed: false);
        ContextCanvas.PropertyChanged += (_, e) =>
        {
            if (e.Property == ZoomBorder.ViewProperty)
            {
                ZoomValue.Text = Labels.ContextMapZoomPct(ContextCanvas.View.Scale);
            }
        };
        ZoomValue.Text = Labels.ContextMapZoomPct(1);
        ContextCanvas.SizeChanged += (_, _) => FitWhenSized();
        SizeChanged += OnStageSized;
    }

    /// <summary>Gets the pannable canvas.</summary>
    internal ZoomBorder Canvas => ContextCanvas;

    /// <summary>Gets the world the canvas holds.</summary>
    internal ContextMapWorld World => ContextWorld;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ContextWorld.Map = DataContext as ContextMapViewModel;
        if (DataContext is ContextMapViewModel map)
        {
            ContextCanvas.WorldBounds = new Rect(0, 0, map.WorldWidth, map.WorldHeight);
            _fit = true;
            FitWhenSized();
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Source is not TextBox && ContextCanvas.HandleZoomKey(e.Key))
        {
            e.Handled = true;
        }
    }

    private static void SetName(Control control, string name) => AutomationProperties.SetName(control, name);

    // The whole map on the stage, once, when the stage has a size (clamped at 62 %, so a narrow window pans to the right).
    private void FitWhenSized()
    {
        if (_fit && ContextCanvas.Bounds.Width > 0 && ContextCanvas.Bounds.Height > 0 && DataContext is ContextMapViewModel)
        {
            _fit = false;
            ContextCanvas.Fit();
        }
    }

    private void OnStageSized(object? sender, SizeChangedEventArgs e)
    {
        HudToggleWidth(e.NewSize.Width);
        if (!_sized && e.NewSize.Width > 0)
        {
            _sized = true;
            SetHud(collapsed: e.NewSize.Width < NarrowStage);
        }
    }

    private void HudToggleWidth(double stage) => ContextHud.Width = stage < NarrowStage ? 300 : 400;

    private void OnHudToggle(object? sender, RoutedEventArgs e) => SetHud(HudBody.IsVisible);

    private void SetHud(bool collapsed)
    {
        HudBody.IsVisible = !collapsed;
        var word = collapsed ? Labels.ContextMapExpand : Labels.ContextMapCollapse;
        AutomationProperties.SetName(HudToggle, word);
        ToolTip.SetTip(HudToggle, word);
        HudChevron.RenderTransform = collapsed ? new Avalonia.Media.RotateTransform(-90) : null;
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e) => ContextCanvas.ZoomIn();

    private void OnZoomOut(object? sender, RoutedEventArgs e) => ContextCanvas.ZoomOut();

    private void OnZoomFit(object? sender, RoutedEventArgs e) => ContextCanvas.Fit();
}
