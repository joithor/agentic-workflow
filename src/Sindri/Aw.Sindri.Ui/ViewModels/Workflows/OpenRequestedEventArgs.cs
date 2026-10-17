using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A Workflows row asked to open its instance.</summary>
/// <param name="link">Where it goes.</param>
/// <param name="intent">What the key that asked wants the frame to do on arrival.</param>
internal sealed class OpenRequestedEventArgs(DeepLink link, FrameIntent intent = FrameIntent.None) : EventArgs
{
    /// <summary>Where the row goes.</summary>
    public DeepLink Link { get; } = link;

    /// <summary>What the frame does on arrival.</summary>
    public FrameIntent Intent { get; } = intent;
}
