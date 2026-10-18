using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>An Agents row asked to open its session's sheet.</summary>
/// <param name="link">Where it goes: session/&lt;provider&gt;/&lt;id&gt;.</param>
internal sealed class SessionOpenRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>The session's link.</summary>
    public DeepLink Link { get; } = link;
}
