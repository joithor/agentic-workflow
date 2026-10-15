namespace Aw.Sindri.Core.Views;

/// <summary>The projector's side of its progress: it publishes its status after each step and takes rebuild requests.</summary>
public interface IProjectorFeed
{
    /// <summary>Publishes the projector's status and wakes the waiters; a rebuild not yet taken keeps showing.</summary>
    /// <param name="status">The status.</param>
    public void Publish(ViewsStatus status);

    /// <summary>True while a rebuild was requested and not yet taken.</summary>
    public bool RebuildRequested { get; }

    /// <summary>True once per rebuild request (RebuildViews) since the last call; clears the request; the projector takes it once the reset has worked, so a failed attempt keeps it.</summary>
    /// <returns>Whether a rebuild was requested.</returns>
    public bool TakeRebuildRequest();
}
