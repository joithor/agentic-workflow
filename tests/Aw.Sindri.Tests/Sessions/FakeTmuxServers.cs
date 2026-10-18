using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Tmux servers by socket path; a socket with no entry is unreachable. It records which sockets were asked.</summary>
internal sealed class FakeTmuxServers : ITmuxServers
{
    private readonly Dictionary<string, TmuxServerView> _servers = new(StringComparer.Ordinal);

    public List<string> Asked { get; } = [];

    public FakeTmuxServers With(string socket, TmuxServerView view)
    {
        _servers[socket] = view;
        return this;
    }

    public Result<TmuxServerView> Read(string socket, CancellationToken cancellationToken)
    {
        Asked.Add(socket);
        return _servers.TryGetValue(socket, out var view) ? view : SindriErrors.SessionSourceFailed.With("tmux");
    }
}
