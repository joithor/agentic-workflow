using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Start;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Start form in the shell (<see cref="ShellViewModel"/>, continued; U5').</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Where the Start form's file inputs are chosen: the window's picker once the app has one; until then none is ever chosen.</summary>
    internal IFilePicker Files { get; init; } = new NullFilePicker();

    /// <summary>The Start form.</summary>
    public StartFormViewModel Start { get; }

    /// <summary>Opens the Start form: from `N` on Workflows, the Workflows head and Home's buttons, ⌘N and the palette.</summary>
    internal void OpenStart()
    {
        Start.Close();
        throw new NotSupportedException();
    }
}
