using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>A running service's View job was pressed: the link that opens the Jobs page on that job's sheet.</summary>
/// <param name="link">The job's link.</param>
internal sealed class JobRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>Where View job goes.</summary>
    public DeepLink Link { get; } = link;
}
