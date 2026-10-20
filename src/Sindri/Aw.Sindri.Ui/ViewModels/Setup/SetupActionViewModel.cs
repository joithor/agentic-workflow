#pragma warning disable MA0041 // red-phase stub: members throw until the implementation commit
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The one action a step offers: a button the app presses itself, or the terminal command to copy.</summary>
internal sealed class SetupActionViewModel
{
    /// <summary>The catalog action id.</summary>
    public string Action => throw new NotSupportedException();

    /// <summary>The button's words.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>The button's accessible name: its words first, then the step.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>True when the app does it itself (a button); false when the person runs the command.</summary>
    public bool IsInApp => throw new NotSupportedException();

    /// <summary>The terminal command with Copy, when the app does not do it itself; else null.</summary>
    public ControlViewModel? Cli => throw new NotSupportedException();

    /// <summary>Presses the button: asks the shell to open it.</summary>
    internal void Open() => throw new NotSupportedException();
}
