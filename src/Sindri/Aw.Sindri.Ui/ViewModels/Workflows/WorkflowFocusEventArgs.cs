namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Asks the Workflows page to put focus on a row's frame (UI spec §3.3: the selection is the focus).</summary>
/// <param name="address">The row's instance address.</param>
/// <param name="onlyIfLost">True when focus moves only if nothing usable has it (a re-read); false when a key asked (J / K).</param>
internal sealed class WorkflowFocusEventArgs(string address, bool onlyIfLost) : EventArgs
{
    /// <summary>The row's instance address.</summary>
    public string Address { get; } = address;

    /// <summary>True when focus moves only if nothing usable has it.</summary>
    public bool OnlyIfLost { get; } = onlyIfLost;
}
