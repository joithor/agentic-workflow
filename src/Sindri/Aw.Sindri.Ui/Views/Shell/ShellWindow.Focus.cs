using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Shell;
using Banner = Aw.Sindri.Ui.ViewModels.Shell.ViewsBanner;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Keeping focus somewhere sensible, and what the window listens to (<see cref="ShellWindow"/>, continued).</summary>
internal sealed partial class ShellWindow
{
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

    // Focus already on the page region would not move on a page change, so the new page's name would go unspoken: drop
    // focus and take it again so the region raises a fresh focus event (review run 4, a11y-2).
    private void KeepFocusOrReannounce()
    {
        var page = this.Get<Border>("Page");
        if (ReferenceEquals(FocusManager?.GetFocusedElement(), page))
        {
            // Avalonia drops focus from an element that is disabled and has no clear call: disable and enable it, then focus again.
            var enabled = page.IsEnabled;
            page.SetCurrentValue(IsEnabledProperty, value: false);
            page.SetCurrentValue(IsEnabledProperty, enabled);
            _ = page.Focus(Method());
            return;
        }

        KeepFocus();
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
