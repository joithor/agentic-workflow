using Aw.Kernel;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILanePane: sessions are scripted per test and the wake lines are kept.</summary>
public sealed class InMemoryLanePane : ILanePane
{
    private readonly Dictionary<string, PaneState> _panes = new(StringComparer.Ordinal);
    private readonly List<(string Session, string Line)> _woken = [];

    /// <summary>Every (session, line) typed, in order.</summary>
    public IReadOnlyList<(string Session, string Line)> Woken => _woken;

    /// <summary>Makes a session exist.</summary>
    /// <param name="session">The session name.</param>
    /// <param name="command">The command running in its pane.</param>
    /// <param name="clients">How many clients are attached.</param>
    public void Put(string session, string command, int clients = 0) => _panes[session] = new PaneState(command, clients);

    /// <summary>Ends a session.</summary>
    /// <param name="session">The session name.</param>
    public void Remove(string session) => _ = _panes.Remove(session);

    /// <inheritdoc />
    public ValueTask<Result<PaneState?>> Probe(string session, CancellationToken cancellationToken) =>
        Done.Of(_panes.GetValueOrDefault(session));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Wake(string session, string line, CancellationToken cancellationToken)
    {
        _woken.Add((session, line));
        return Done.Unit();
    }
}
