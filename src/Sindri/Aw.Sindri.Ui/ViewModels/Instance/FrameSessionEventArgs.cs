namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame's agent Step asks to open its session's sheet on the Agents page.</summary>
/// <param name="sessionRef">The session's ref, provider/id.</param>
internal sealed class FrameSessionEventArgs(string sessionRef) : EventArgs
{
    /// <summary>The session's ref.</summary>
    public string Ref { get; } = sessionRef;
}
