using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILaneSessions: sessions are scripted per test, or changed by a hook when a test needs a /clear to take effect.</summary>
public sealed class InMemoryLaneSessions : ILaneSessions
{
    private readonly Dictionary<string, SessionId> _current = new(StringComparer.Ordinal);

    /// <summary>Sets the id the lane's Claude runs now.</summary>
    /// <param name="session">The tmux session name.</param>
    /// <param name="id">The session id.</param>
    public void Set(string session, string id) => _current[session] = SessionId.From(id);

    /// <inheritdoc />
    public ValueTask<Result<SessionId?>> Current(string session, CancellationToken cancellationToken) =>
        Done.Of<SessionId?>(_current.TryGetValue(session, out var id) ? id : null);
}
