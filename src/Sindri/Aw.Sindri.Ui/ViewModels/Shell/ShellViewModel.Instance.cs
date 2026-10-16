#pragma warning disable CA1822, MA0041, IDE0060 // red stub: the members answer nothing until the green commit
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The instance frame in the shell (<see cref="ShellViewModel"/>, continued; U8 T4).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The instance frame.</summary>
    public InstanceFrameViewModel Instance { get; }

    /// <summary>True when the sheet's subject is a workflow instance.</summary>
    public bool IsInstanceSheet => false;

    /// <summary>The sheet's width as it is shown: an instance's is the widest, the others the human's.</summary>
    public double ShownSheetWidth => SheetWidth;

    /// <summary>The Workflows row the frame was opened from (its address), where Esc returns focus.</summary>
    internal string? OpenerWorkflow { get; private set; }

    /// <summary>Opens an instance's frame.</summary>
    /// <param name="address">The address.</param>
    /// <param name="id">Its id when known.</param>
    internal void OpenInstance(string address, string? id)
    {
    }

    private void InitInstance()
    {
    }
}
