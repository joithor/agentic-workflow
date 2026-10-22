namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>A hold that reached the server's time.</summary>
internal sealed class HeldEventArgs(int heldMs) : EventArgs
{
    /// <summary>How long the button was held, in milliseconds.</summary>
    internal int HeldMs { get; } = heldMs;
}
