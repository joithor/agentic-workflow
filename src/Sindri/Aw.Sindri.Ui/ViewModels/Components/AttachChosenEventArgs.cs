namespace Aw.Sindri.Ui.ViewModels.Components;

/// <summary>An action the Attach component asks for (it sends nothing itself; a page that hosts it does).</summary>
/// <param name="action">What was chosen.</param>
internal sealed class AttachChosenEventArgs(AttachAction action) : EventArgs
{
    /// <summary>What was chosen.</summary>
    public AttachAction Action { get; } = action;
}
