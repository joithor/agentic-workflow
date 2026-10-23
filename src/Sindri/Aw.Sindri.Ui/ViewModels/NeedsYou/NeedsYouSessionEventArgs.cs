namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The session link was followed.</summary>
/// <param name="sessionRef">The session's ref (provider/id).</param>
internal sealed class NeedsYouSessionEventArgs(string sessionRef) : EventArgs
{
    /// <summary>The session's ref.</summary>
    public string SessionRef { get; } = sessionRef;
}
