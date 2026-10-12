using Aw.Kernel;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// The projector's progress in memory: the projector publishes its status here, queries wait on it and RebuildViews
/// requests a rebuild through it. A stub until P2 unit B (ProjectorStatusContract is red until then).
/// </summary>
internal sealed class ProjectorProgress : IProjectorStatus, IProjectorControl
{
    /// <inheritdoc/>
    public ViewsStatus Current => throw new NotSupportedException("ProjectorProgress: P2 unit B makes ProjectorStatusContract pass");

    /// <summary>Publishes the projector's status and wakes the waiters.</summary>
    /// <param name="status">The status.</param>
    public void Publish(ViewsStatus status) => throw new NotSupportedException("ProjectorProgress: P2 unit B makes ProjectorStatusContract pass");

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitFor(long sequence, TimeSpan limit, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ProjectorProgress: P2 unit B makes ProjectorStatusContract pass");

    /// <inheritdoc/>
    public void RequestRebuild() => throw new NotSupportedException("ProjectorProgress: P2 unit B makes ProjectorStatusContract pass");
}
