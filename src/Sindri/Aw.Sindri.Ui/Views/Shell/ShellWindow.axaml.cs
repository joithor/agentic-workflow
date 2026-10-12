using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The shell window (UI spec §3.2): binds to the shell view model. The code here is view plumbing only: the keys of §8
/// that U0 serves (F6 cycles top bar, page and sheet; Esc closes the sheet; ⌘, opens Settings), the sheet's drag and its
/// covering below 1100 px, handing the window's bounds and the sheet's width to the view model to remember, and keeping
/// focus somewhere sensible (§11.2) when the focused control disappears: closing the sheet returns it to what opened the
/// sheet (the edge tab) or the page region, and a state change that hides it moves it to its region's neighbour (the
/// health pill in the top bar, the page region in the page). It never goes nowhere.
/// </summary>
internal sealed partial class ShellWindow : Window
{
    private static readonly string[] _regions = ["TopBar", "Page", "Sheet"];

    // ⌘ on macOS, Ctrl elsewhere (§8: ⌘, opens Settings).
    private static readonly KeyModifiers _command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private ShellViewModel? _shell;
    private Control? _lastFocused;
    private NavigationMethod _lastMethod = NavigationMethod.Tab;
    private Control? _sheetOpener;

    public ShellWindow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, OnGotFocus, handledEventsToo: true);
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    private Border SheetPanel => this.Get<Border>("Sheet");

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        _shell?.PropertyChanged -= OnShellChanged;
        _shell = Shell;
        _shell?.PropertyChanged += OnShellChanged;
        if (_shell?.Window is { } bounds)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = PixelPoint.FromPoint(new Point(bounds.X, bounds.Y), DesktopScaling);
            Width = bounds.Width;
            Height = bounds.Height;
        }

        Cover(Bounds.Width);
        base.OnDataContextChanged(e);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        Cover(e.NewSize.Width);
        base.OnSizeChanged(e);
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        var at = Position.ToPoint(DesktopScaling);
        Shell?.RememberWindow(new WindowBounds(at.X, at.Y, ClientSize.Width, ClientSize.Height));
        base.OnClosing(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F6)
        {
            e.Handled = CycleRegion(back: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
        else if (e.Key == Key.Escape && Shell is { IsSheetOpen: true } shell)
        {
            shell.CloseSheet();
            e.Handled = true;
        }
        else if (e.Key == Key.OemComma && e.KeyModifiers == _command && Shell is { } open)
        {
            open.GoTo(PageId.Settings);
            // Like the sheet's heading (§11.2), opening Settings moves focus into it: its first control.
            Dispatcher.UIThread.Post(() => FirstFocusable(this.Get<Control>("SettingsSlot"))?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    // F6 (§11.2): the next region (top bar, page, sheet when open) gets focus: the region itself, so Tab then moves
    // inside it (or its first focusable control when the region itself can't take focus).
    private bool CycleRegion(bool back)
    {
        Control[] regions = [.. _regions.Select(this.Get<Control>).Where(c => c.IsEffectivelyVisible)];
        var focused = FocusManager?.GetFocusedElement() as Visual;
        var current = Array.FindIndex(regions, r => focused is not null && (r == focused || r.IsVisualAncestorOf(focused)));
        var next = regions[(current + (back ? regions.Length - 1 : 1) + regions.Length) % regions.Length];
        var target = next.Focusable ? next : FirstFocusable(next) ?? next;
        return target.Focus(NavigationMethod.Tab);
    }

    private static InputElement? FirstFocusable(Visual within) => within.GetVisualDescendants().OfType<InputElement>()
        .FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);

    // Below Size.SheetCoversBelow the sheet covers the page, with a back affordance; otherwise it keeps its width.
    private void Cover(double width)
    {
        var below = this.TryFindResource("Size.SheetCoversBelow", out var value) && value is double limit ? limit : 1100;
        var covers = width > 0 && width < below;
        SheetPanel.Classes.Set("covers", covers);
        SheetPanel.Width = covers ? double.NaN : Shell?.SheetWidth ?? double.NaN;
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.SheetWidth))
        {
            Cover(Bounds.Width);
        }
        else if (e.PropertyName == nameof(ShellViewModel.State) || (e.PropertyName == nameof(ShellViewModel.IsSheetShown) && Shell is { IsSheetShown: false }))
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
        else if (e.PropertyName == nameof(ShellViewModel.IsSheetShown) && Shell is { IsSheetShown: true } && SheetPanel.Classes.Contains("covers"))
        {
            // Covering the page, opening the sheet moves focus to its heading (§11.2).
            Dispatcher.UIThread.Post(() => this.Get<TextBlock>("SheetTitle").Focus(), DispatcherPriority.Loaded);
        }
    }

    private void OnSheetDrag(object? sender, VectorEventArgs e) => SheetPanel.Width = SheetPanel.Bounds.Width - e.Vector.X;

    private void OnSheetDragged(object? sender, VectorEventArgs e) => Shell?.ResizeSheet(SheetPanel.Bounds.Width);

    private void OnCloseSheet(object? sender, RoutedEventArgs e) => Shell?.CloseSheet();

    private void OnEdgeTab(object? sender, RoutedEventArgs e)
    {
        _sheetOpener = this.Get<Button>("EdgeTab");
        Shell?.ReopenSheet();
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is Control control)
        {
            _lastFocused = control;
            _lastMethod = e.NavigationMethod;
        }
    }

    // After a state change or a closed sheet: if the focused control is gone (hidden, disabled, or nothing has focus
    // after something had it), focus its region's neighbour.
    private void KeepFocus()
    {
        if (FocusManager?.GetFocusedElement() is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } || _lastFocused is not { } last)
        {
            return;
        }

        var target = Neighbour(last) ?? Usable(FirstFocusable(TopBar) as Control);
        _sheetOpener = null;
        _ = target?.Focus(_lastMethod == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab);
    }

    // The sheet's opener (the edge tab) or the page region for the sheet; the health pill for the top bar; the page region.
    private Control? Neighbour(Control last)
    {
        var page = Usable(this.Get<Control>("Page"));
        if (SheetPanel == last || SheetPanel.IsVisualAncestorOf(last))
        {
            return Usable(_sheetOpener) ?? page;
        }

        return TopBar.IsVisualAncestorOf(last) ? Usable(TopBar.Get<Control>("HealthPill")) : page;
    }

    private static Control? Usable(Control? control) =>
        control is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true } ? control : null;
}
