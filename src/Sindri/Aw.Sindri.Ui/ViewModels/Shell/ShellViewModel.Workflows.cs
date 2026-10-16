using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Workflows page in the shell (<see cref="ShellViewModel"/>, continued; U8 T2).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Workflows page's list.</summary>
    public WorkflowsViewModel Workflows => field ??= new WorkflowsViewModel(_api);
}
