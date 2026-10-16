using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus on Home's Needs-you rows (<see cref="ShellWindow"/>, continued; UI spec §11.2): the selection is the focus, and focus is never lost.</summary>
internal sealed partial class ShellWindow
{
    private void ListenNeedsYou(ShellViewModel shell, bool on)
    {
        if (on)
        {
            shell.NeedsYou.FocusRequested += OnNeedsYouFocus;
            shell.NeedsYou.PropertyChanged += OnNeedsYouChanged;
            shell.NeedsYou.Seen.PropertyChanged += OnSeenChanged;
        }
        else
        {
            shell.NeedsYou.FocusRequested -= OnNeedsYouFocus;
            shell.NeedsYou.PropertyChanged -= OnNeedsYouChanged;
            shell.NeedsYou.Seen.PropertyChanged -= OnSeenChanged;
        }
    }

    // Esc closed a row's sheet: focus goes back to the row it was opened from.
    private void OnNeedsYouFocus(object? sender, NeedsYouRowEventArgs request) =>
        Dispatcher.UIThread.Post(() => FocusNeedsYouRow(request.RowId), DispatcherPriority.Loaded);

    // J / K (or the row after a cleared one) moved the selection: the row's frame takes focus, on the keyboard's ring.
    private void OnNeedsYouChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(NeedsYouViewModel.SelectedId), StringComparison.Ordinal))
        {
            return;
        }

        // No row left to select (the last one cleared): focus still goes somewhere (the page region).
        var id = Shell?.NeedsYou.SelectedId;
        Dispatcher.UIThread.Post(() => (id is null ? KeepFocus : (Action)(() => FocusNeedsYouRow(id)))(), DispatcherPriority.Loaded);
    }

    // The Undo offer ends (pressed, or ten seconds): a focus on its button moves on.
    private void OnSeenChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NeedsYouSeenViewModel.UndoOffered), StringComparison.Ordinal) && Shell?.NeedsYou.Seen.UndoOffered == false)
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
    }

    private void FocusNeedsYouRow(string rowId)
    {
        var row = this.Get<Border>("Page").GetVisualDescendants().OfType<NeedsYouRow>()
            .FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.RowId, rowId, StringComparison.Ordinal));
        if (row?.IsKeyboardFocusWithin == true)
        {
            return;   // focus is already in the row (a click on its title or a control): it stays where it is
        }

        var frame = row?.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal));
        if (frame is null)
        {
            KeepFocus();   // the row is not drawn: focus still goes somewhere
            return;
        }

        _ = frame.Focus(NavigationMethod.Tab);
    }
}
