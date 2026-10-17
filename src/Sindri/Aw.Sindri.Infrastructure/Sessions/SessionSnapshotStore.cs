using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The in-memory snapshot (collector spec CI-3): written by the collector's pass, read by queries.</summary>
internal sealed class SessionSnapshotStore : ISessionSnapshot
{
    private volatile SessionSnapshot? _current;

    public SessionSnapshot? Current => _current;

    public void Publish(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _current = snapshot;
    }
}
