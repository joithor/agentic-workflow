using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Queue;
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

        if (on)
        {
            shell.Queue.FocusRequested += OnFocusRow;
        }
        else
        {
            shell.Queue.FocusRequested -= OnFocusRow;
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

    // The queue asks for focus on a row (J / K, or the row a refresh moved the selection to): the row's frame gets it, on
    // the keyboard's ring, once the refresh has laid the rows out. A refresh only takes it when focus was lost with the old row.
    private void OnFocusRow(object? sender, FocusRequestedEventArgs request) =>
        Dispatcher.UIThread.Post(() => FocusRow(request), DispatcherPriority.Loaded);

    private void FocusRow(FocusRequestedEventArgs request)
    {
        if (request.OnlyIfLost && HasUsableFocus)
        {
            return;
        }

        _ = RowFrame(request.ItemId)?.Focus(NavigationMethod.Tab);
    }

    // The visible frame of the row for an item, on the page the window shows.
    private Control? RowFrame(string itemId) => Usable(this.Get<Control>("Page").GetVisualDescendants().OfType<Border>()
        .FirstOrDefault(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal) && b.IsEffectivelyVisible
            && b.DataContext is ItemRowViewModel row && string.Equals(row.Id, itemId, StringComparison.Ordinal)));

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

    // The sheet's opener (the row it was opened from, by id, or the edge tab) or the page region for the sheet; the health pill for the top bar; the page
    // region, or the sheet's heading while the sheet covers the page.
    private Control? Neighbour(Control last)
    {
        var page = Usable(this.Get<Control>("Page")) ?? (Covering ? Usable(this.Get<TextBlock>("SheetTitle")) : null);
        if (SheetPanel == last || SheetPanel.IsVisualAncestorOf(last))
        {
            return (Shell?.OpenerRow is { } id ? RowFrame(id) : null) ?? Usable(_sheetOpener) ?? page;
        }

        return TopBar.IsVisualAncestorOf(last) ? Usable(TopBar.Get<Control>("HealthPill")) : page;
    }

    private static Control? Usable(Control? control) =>
        control is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true } ? control : null;
}
