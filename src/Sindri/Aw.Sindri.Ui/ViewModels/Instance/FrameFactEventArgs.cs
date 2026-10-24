namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>A request to show one fact in the frame's facts panel.</summary>
/// <param name="fact">The fact's key.</param>
internal sealed class FrameFactEventArgs(string fact) : EventArgs
{
    /// <summary>The fact's key ("fact:design").</summary>
    public string Fact { get; } = fact;
}
