namespace Aw.Sindri.Core.Views;

/// <summary>Asks the projector to rebuild (RebuildViews).</summary>
public interface IProjectorControl
{
    /// <summary>Requests a rebuild; the status shows rebuilding at once.</summary>
    public void RequestRebuild();
}
