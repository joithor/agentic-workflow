using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Setup card and page in the shell (<see cref="ShellViewModel"/>, continued; setup-once spec §5.7).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The setup list behind Home's card and the Setup page.</summary>
    public SetupViewModel Setup { get; }

    /// <summary>True on Setup.</summary>
    public bool IsSetup => CurrentPage == PageId.Setup;
}
