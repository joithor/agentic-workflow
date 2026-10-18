namespace Aw.Sindri.Core.Sessions;

/// <summary>The bound unix sockets of the system (/proc/net/unix): the way a tmux server's socket path is found without a tmux call.</summary>
public interface IUnixSockets
{
    /// <summary>Inode to path for every socket that has a path.</summary>
    /// <returns>The map; empty when the table can't be read.</returns>
    public IReadOnlyDictionary<long, string> Read();
}
