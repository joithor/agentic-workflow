using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Workflows page in the shell (<see cref="ShellViewModel"/>, continued; U8 T2): read with every event, whatever page is visible; its open figure is the tab's count.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Workflows page's list.</summary>
    public WorkflowsViewModel Workflows { get; }

    // Everything the page needs once it exists (ShellViewModel's constructor calls this one line).
    private void InitWorkflows()
    {
        Health.Follow(Workflows.Refresh);
        Workflows.OpenRequested += (_, e) => Navigate(e.Link);
        Workflows.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(WorkflowsViewModel.NavCount), StringComparison.Ordinal))
            {
                Navigation.First(n => n.Page == PageId.Workflows).Count = Workflows.NavCount;
            }
        };
        Health.Banner.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ViewsBanner.IsStalled), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(ViewsBanner.IsRebuilding), StringComparison.Ordinal))
            {
                Workflows.SetStale(Health.Banner.IsStalled || Health.Banner.IsRebuilding);
            }
        };
    }
}
