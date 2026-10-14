namespace Aw.Sindri.Core.Views;

/// <summary>The projector's side of its progress: it publishes its status after each step and takes rebuild requests.</summary>
public interface IProjectorFeed
{
    /// <summary>Publishes the projector's status and wakes the waiters; a rebuild not yet taken keeps showing.</summary>
    /// <param name="status">The status.</param>
    public void Publish(ViewsStatus status);

    /// <summary>True once per rebuild request (RebuildViews) since the last call; clears the request.</summary>
    /// <returns>Whether a rebuild was requested.</returns>
    public bool TakeRebuildRequest();
}
