using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus on Home's Workflows strip (<see cref="ShellWindow"/>, continued; UI spec §11.2): the selection is the focus, and focus is never lost.</summary>
internal sealed partial class ShellWindow
{
    private void ListenStrip(ShellViewModel shell, bool on)
    {
        if (on)
        {
            shell.Strip.PropertyChanged += OnStripChanged;
        }
        else
        {
            shell.Strip.PropertyChanged -= OnStripChanged;
        }
    }

    // J / K moved the selection onto a strip row: its frame takes focus, on the keyboard's ring. A selection that cleared (the
    // row finished, or another list took it) leaves focus where it is; the other list's own move puts it there.
    private void OnStripChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(WorkflowsStripViewModel.SelectedAddress), StringComparison.Ordinal) || Shell?.Strip.SelectedAddress is not { } address)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => FocusStripRow(address), DispatcherPriority.Loaded);
    }

    private void FocusStripRow(string address)
    {
        var row = this.Get<Border>("Page").GetVisualDescendants().OfType<StripRow>()
            .FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.Address, address, StringComparison.Ordinal));
        if (row?.IsKeyboardFocusWithin == true)
        {
            return;   // focus is already in the row (a click or its control): it stays where it is
        }

        var frame = row?.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "StripRowFrame", StringComparison.Ordinal));
        if (frame is null)
        {
            KeepFocus();   // the row is not drawn: focus still goes somewhere
            return;
        }

        _ = frame.Focus(NavigationMethod.Tab);
    }
}
