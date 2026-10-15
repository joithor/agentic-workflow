using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The shell window (UI spec §3.2): binds to the shell view model. The code here is view plumbing only: the keys of §8
/// that U0 serves (F6 and Shift+F6 cycle top bar, page and sheet, and on macOS ⌘⌥→ and ⌘⌥← too; Esc closes the sheet,
/// or Settings over the Not running card; ⌘, opens Settings), the sheet's drag and its covering below 1100 px, handing
/// the window's bounds and the sheet's width to the view model to remember, and keeping focus somewhere sensible (§11.2)
/// when the focused control disappears: closing the sheet returns it to what opened the sheet (the edge tab) or the page
/// region, and a state change that hides it moves it to its region's neighbour (the health pill in the top bar, the page
/// region in the page). It never goes nowhere.
/// </summary>
internal sealed partial class ShellWindow : Window
{
    private static readonly string[] _regions = ["TopBar", "Page", "Sheet"];

    // ⌘ on macOS, Ctrl elsewhere (§8: ⌘, opens Settings).
    private static readonly KeyModifiers _command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private ShellViewModel? _shell;
    private Control? _lastFocused;
    private int _rowRequests;   // row-focus requests posted and not yet run
    private NavigationMethod _lastMethod = NavigationMethod.Tab;
    private Control? _sheetOpener;

    // What had focus when the palette or the ? sheet opened: it gets focus back when they close (§8, §11.2).
    private Control? _overlayOpener;

    public ShellWindow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, OnGotFocus, handledEventsToo: true);
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    private Border SheetPanel => this.Get<Border>("Sheet");

    private Border PagePanel => this.Get<Border>("Page");

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        Listen(_shell, on: false);
        _shell = Shell;
        Listen(_shell, on: true);
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
        RememberBounds();
        base.OnClosing(e);
    }

    /// <summary>Hands the window's bounds to the view model to remember (on closing, and before the app ends).</summary>
    internal void RememberBounds()
    {
        var at = Position.ToPoint(DesktopScaling);
        Shell?.RememberWindow(new WindowBounds(at.X, at.Y, ClientSize.Width, ClientSize.Height));
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = RegionStep(e.Key, e.KeyModifiers, OperatingSystem.IsMacOS()) is { } step ? CycleRegion(back: step < 0)
            : e.Key == Key.Escape ? Escape()
            : TabOutOfOpenRow(e) || OpenByKey(e) || PageKey(e);
        base.OnKeyDown(e);
    }

    // Esc closes the sheet when it shows, or Settings over the Not running card (focus then goes from its hidden control
    // to the page region); otherwise it does nothing here. A sheet hidden while Sindri isn't running stays open, so the
    // keypress isn't spent on what the user can't see and the sheet is back when Sindri is.
    private bool Escape()
    {
        if (Shell is not { } shell)
        {
            return false;
        }

        if (shell.Palette.IsOpen)
        {
            shell.Palette.Close();
            return true;
        }

        if (shell.Shortcuts.IsOpen)
        {
            shell.Shortcuts.Close();
            return true;
        }

        if (EscapeItemLayer(shell))
        {
            return true;
        }

        if (shell.IsSheetShown)
        {
            shell.CloseSheet();
            return true;
        }

        if (!shell.LeaveSettings())
        {
            return false;
        }

        Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        return true;
    }

    // F6, or ⌘⌥→ and ⌘⌥← on macOS (§8, §11.2): the next region (top bar, page, sheet when open) gets focus: the
    // region itself, so Tab then moves inside it (or its first focusable control when the region itself can't take focus).
    private bool CycleRegion(bool back)
    {
        if (OverlayOpen)
        {
            return true;   // the palette and the ? sheet trap focus: the regions behind the scrim are out of reach (WCAG 2.4.3)
        }

        Control[] regions = [.. _regions.Select(this.Get<Control>).Where(c => c.IsEffectivelyVisible && c.IsEffectivelyEnabled)];
        var focused = FocusManager?.GetFocusedElement() as Visual;
        var current = Array.FindIndex(regions, r => focused is not null && (r == focused || r.IsVisualAncestorOf(focused)));
        var next = regions[(current + (back ? regions.Length - 1 : 1) + regions.Length) % regions.Length];
        var target = next.Focusable ? next : FirstFocusable(next) ?? next;
        return target.Focus(NavigationMethod.Tab);
    }

    /// <summary>The step a key takes through the regions (§8, §11.2): 1 forward, -1 back, null when it isn't a region key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">Its modifiers.</param>
    /// <param name="macOS">Whether the app runs on macOS.</param>
    /// <returns>The step, or null.</returns>
    internal static int? RegionStep(Key key, KeyModifiers modifiers, bool macOS) => (key, modifiers) switch
    {
        (Key.F6, KeyModifiers.None) => 1,
        (Key.F6, KeyModifiers.Shift) => -1,
        (Key.Right, KeyModifiers.Meta | KeyModifiers.Alt) when macOS => 1,
        (Key.Left, KeyModifiers.Meta | KeyModifiers.Alt) when macOS => -1,
        _ => null,
    };

    private static InputElement? FirstFocusable(Visual within) => within.GetVisualDescendants().OfType<InputElement>()
        .FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);

    // Below Size.SheetCoversBelow the sheet covers the page, with a back affordance; otherwise it keeps its width. When
    // an open sheet starts or stops covering (the window narrowed or widened), focus left on the page it now disables
    // moves on (review run 4, accessibility-tester-1).
    private void Cover(double width)
    {
        var below = this.TryFindResource("Size.SheetCoversBelow", out var value) && value is double limit ? limit : 1100;
        var covers = width > 0 && width < below;
        var changed = covers != SheetPanel.Classes.Contains("covers");
        SheetPanel.Classes.Set("covers", covers);
        SheetPanel.Width = covers ? double.NaN : Shell?.SheetWidth ?? double.NaN;
        CoverPage();
        if (changed && Shell is { IsSheetShown: true })
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
    }

    // While the sheet covers the page, the page and its controls leave the tab order, F6's cycle and hit-testing, so focus
    // never lands where it can't be seen (WCAG 2.4.3, 2.4.7). The sheet hides the page's disabled look. The page is
    // enabled again as soon as the sheet closes or stops covering, before focus returns to it (KeepFocus runs later).
    private void CoverPage() => PagePanel.IsEnabled = !(Covering && Shell is { IsSheetShown: true });

    private bool Covering => SheetPanel.Classes.Contains("covers");

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.SheetWidth):
                Cover(Bounds.Width);
                break;
            case nameof(ShellViewModel.State):
                CoverPage();
                Dispatcher.UIThread.Post(Shell is { IsVersionMismatch: true } ? FocusMismatchCard : KeepFocus, DispatcherPriority.Loaded);
                break;
            case nameof(ShellViewModel.IsSheetShown):
                CoverPage();
                Dispatcher.UIThread.Post(Shell is { IsSheetShown: true } ? FocusOpenedSheet : KeepFocus, DispatcherPriority.Loaded);
                break;
            default:
                break;
        }
    }

    // Opening the sheet over the page (below 1100 px) moves focus to its heading (§11.2). Beside the page focus stays
    // where it is, unless what had it hid as the sheet opened (the edge tab at 1100 px and wider; review run 4,
    // accessibility-tester-0): then the heading gets it too, never a hidden control.
    private void FocusOpenedSheet()
    {
        if (Shell is { IsSheetShown: true } && (Covering || !HasUsableFocus))
        {
            _ = this.Get<TextBlock>("SheetTitle").Focus(Method());
        }
    }

    // The version-mismatch card replaces the whole shell (it can appear mid-session), so whatever had focus is hidden and
    // there is no neighbour: focus goes into the card, to its heading, which is spoken with the card's live region
    // (review run 5, accessibility-tester-0).
    private void FocusMismatchCard()
    {
        if (Shell is { IsVersionMismatch: true })
        {
            _ = this.Get<TextBlock>("VersionMismatchText").Focus(Method());
        }
    }

    private bool HasUsableFocus => FocusManager?.GetFocusedElement() is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true };

    // A keyboard user's focus moves with its ring (Tab); a pointer user's without.
    private NavigationMethod Method() => _lastMethod == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab;

    private void OnSheetDrag(object? sender, VectorEventArgs e) =>
        SheetPanel.Width = Math.Clamp(SheetPanel.Bounds.Width - e.Vector.X, SheetPanel.MinWidth, SheetPanel.MaxWidth);

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
}
