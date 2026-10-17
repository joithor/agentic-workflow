using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Testing;

/// <summary>The snapshot in memory. Kept honest by <see cref="SessionSnapshotContract"/>.</summary>
public sealed class InMemorySessionSnapshot : ISessionSnapshot
{
    /// <inheritdoc/>
    public SessionSnapshot? Current { get; private set; }

    /// <inheritdoc/>
    public void Publish(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Current = snapshot;
    }
}
