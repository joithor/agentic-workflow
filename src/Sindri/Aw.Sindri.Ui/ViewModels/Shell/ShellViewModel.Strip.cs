using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Home's Workflows strip in the shell (<see cref="ShellViewModel"/>, continued; U3' T17): its keys, its links and the inline answer.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Home's Workflows strip.</summary>
    public WorkflowsStripViewModel Strip { get; }

    // A and Enter on the strip's selected row: stubbed red (T17).
#pragma warning disable CA1822, IDE0060 // red stub, replaced by the green commit
    private bool StripKeyPressed(string key, bool inSheet) => false;
#pragma warning restore CA1822, IDE0060
}
