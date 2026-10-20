using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The approve-all dialog in the shell (<see cref="ShellViewModel"/>, continued): opened by the setup row's button, the palette and the catalog's ready row.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The approve-all dialog.</summary>
    public ApproveAllViewModel ApproveAll { get; }

    /// <summary>Opens the dialog: it reads the plan and asks for the challenge. Does nothing when the catalog does not let the app approve.</summary>
    internal void OpenApproveAll() => _ = ApproveAll.Open(CancellationToken.None);
}
