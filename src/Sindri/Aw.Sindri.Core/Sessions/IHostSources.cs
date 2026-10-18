namespace Aw.Sindri.Core.Sessions;

/// <summary>What <see cref="HostRules"/> asks the world for during one pass: terminals and teams, a process's environment, a tmux server's socket and view.</summary>
public interface IHostSources
{
    /// <summary>The terminals known by process name.</summary>
    public IReadOnlyList<TerminalName> Terminals { get; }

    /// <summary>The Meta teams.</summary>
    public IReadOnlyList<TeamNames> Teams { get; }

    /// <summary>The allowlisted environment of <paramref name="pid"/>; empty when unreadable.</summary>
    /// <param name="pid">The process.</param>
    /// <returns>The environment.</returns>
    public IReadOnlyDictionary<string, string> Environment(int pid);

    /// <summary>The socket path a tmux server listens on, from its open files and the unix socket table.</summary>
    /// <param name="serverPid">The server process.</param>
    /// <returns>The path, or null when it can't be found.</returns>
    public string? SocketOf(int serverPid);

    /// <summary>The panes and clients of the server on <paramref name="socket"/>.</summary>
    /// <param name="socket">The socket path.</param>
    /// <returns>The view, or null when the server can't be read.</returns>
    public TmuxServerView? View(string socket);
}
