using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A Workflows row asked to open its instance.</summary>
/// <param name="link">Where it goes.</param>
internal sealed class OpenRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>Where the row goes.</summary>
    public DeepLink Link { get; } = link;
}
