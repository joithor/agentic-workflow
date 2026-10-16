using System.ComponentModel;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus on the Workflows page (<see cref="ShellWindow"/>, continued; UI spec §11.2): the page moves focus with J / K itself; here, a selection that cleared leaves focus somewhere usable.</summary>
internal sealed partial class ShellWindow
{
    private void ListenWorkflows(ShellViewModel shell, bool on)
    {
        if (on)
        {
            shell.Workflows.PropertyChanged += OnWorkflowsChanged;
        }
        else
        {
            shell.Workflows.PropertyChanged -= OnWorkflowsChanged;
        }
    }

    // The selected row went with a re-read (it finished, or a filter dropped it): the control that had focus is gone, so focus goes to the page's
    // neighbour, never to nowhere. A selection that moved is the page's own to follow.
    private void OnWorkflowsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkflowsViewModel.SelectedAddress), StringComparison.Ordinal) && Shell?.Workflows.SelectedAddress is null)
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
    }
}
