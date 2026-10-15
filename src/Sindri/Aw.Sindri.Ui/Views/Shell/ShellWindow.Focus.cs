using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Shell;
using Banner = Aw.Sindri.Ui.ViewModels.Shell.ViewsBanner;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Keeping focus somewhere sensible, and what the window listens to (<see cref="ShellWindow"/>, continued).</summary>
internal sealed partial class ShellWindow
{
    // Right, Left, Home or End pressed with focus on a nav tab selects a tab by itself (a tabs pattern with automatic
    // activation, review re-check Y1): the page changes and focus stays on the tab. Any other key, or a pointer press, clears it.
    private bool _navArrowing;

    private void WatchNavigationKeys()
    {
        AddHandler(KeyDownEvent, OnNavigationKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => _navArrowing = false, RoutingStrategies.Tunnel);
    }

    private void OnNavigationKeyDown(object? sender, KeyEventArgs e) =>
        _navArrowing = e is { Key: Key.Left or Key.Right or Key.Home or Key.End, KeyModifiers: KeyModifiers.None }
            && FocusManager?.GetFocusedElement() is Visual focused
            && TopBar.Get<ListBox>("Navigation").IsVisualAncestorOf(focused);

    private void Listen(ShellViewModel? shell, bool on)
    {
        if (shell is null)
        {
            return;
        }

        PropertyChangedEventHandler[] handlers = [OnShellChanged, OnBannerChanged, OnOverlayChanged, OnOverlayChanged];
        INotifyPropertyChanged[] sources = [shell, shell.Health.Banner, shell.Palette, shell.Shortcuts];
        for (var i = 0; i < sources.Length; i++)
        {
            if (on)
            {
                sources[i].PropertyChanged += handlers[i];
            }
            else
            {
                sources[i].PropertyChanged -= handlers[i];
            }
        }
    }

    // The views banner carries Rebuild views: when it goes, or the control goes with a change of banner, focus on it moves on.
    private void OnBannerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(Banner.IsShown), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(Banner.HasRebuild), StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
    }

    // A page change made by a navigation command (a G-chord, the palette's Go to, a nav tab) always lands focus on the new
    // page's region, wherever focus was: the top bar, a nav item, a sheet beside the page (review run 5, a11y-0). Focus
    // already on the region would not move, so it is dropped and taken again to raise a fresh event for the new name
    // (run 4, a11y-2). Over a covering sheet the page is disabled and cannot take focus: the usual keep-focus rule applies.
    private void KeepFocusOrReannounce()
    {
        var page = this.Get<Border>("Page");
        if (!page.IsEffectivelyEnabled)
        {
            KeepFocus();
            return;
        }

        if (ReferenceEquals(FocusManager?.GetFocusedElement(), page))
        {
            // Avalonia drops focus from an element that is disabled and has no clear call: disable and enable it, then focus again.
            var enabled = page.IsEnabled;
            page.SetCurrentValue(IsEnabledProperty, value: false);
            page.SetCurrentValue(IsEnabledProperty, enabled);
        }

        _sheetOpener = null;
        _ = page.Focus(Method());
    }

    // After a state change, a closed sheet or a change in its covering: if the focused control is gone (hidden, disabled,
    // or nothing has focus after something had it), focus its region's neighbour.
    private void KeepFocus()
    {
        if (HasUsableFocus || _lastFocused is not { } last)
        {
            return;
        }

        var target = Neighbour(last) ?? Usable(FirstFocusable(TopBar) as Control);
        _sheetOpener = null;
        _ = target?.Focus(Method());
    }

    // The sheet's opener (the edge tab) or the page region for the sheet; the health pill for the top bar; the page
    // region, or the sheet's heading while the sheet covers the page.
    private Control? Neighbour(Control last)
    {
        var page = Usable(this.Get<Control>("Page")) ?? (Covering ? Usable(this.Get<Border>("SheetTitle")) : null);
        if (SheetPanel == last || SheetPanel.IsVisualAncestorOf(last))
        {
            return Usable(_sheetOpener) ?? page;
        }

        return TopBar.IsVisualAncestorOf(last) ? Usable(TopBar.Get<Control>("HealthPill")) : page;
    }

    private static Control? Usable(Control? control) =>
        control is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true } ? control : null;
}
