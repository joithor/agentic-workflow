using Aw.Kernel;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Read-only questions to a tmux server (collector spec CI-3): the only way Sindri talks to tmux, through the four list/display verbs.</summary>
public interface ITmuxServers
{
    /// <summary>The panes and clients of the server on <paramref name="socket"/>, answered from a 10 s cache.</summary>
    /// <param name="socket">The server's socket path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The view, or an error when tmux is missing, the server doesn't answer, or the socket timed out in the last 10 s.</returns>
    public Result<TmuxServerView> Read(string socket, CancellationToken cancellationToken);
}
