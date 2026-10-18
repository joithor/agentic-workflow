namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// Reads one pane's text for the live preview (collector spec CA-9, CI-5): the pane's first process and then its last 40 lines, on the socket the session
/// was found on, within one 1 s deadline. A read, never a change to the server.
/// </summary>
public interface IPaneReader
{
    /// <summary>Asks the server on <paramref name="socket"/> for <paramref name="pane"/>'s first process and text.</summary>
    /// <param name="socket">The server's absolute socket path.</param>
    /// <param name="pane">The pane id, '%n'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The read; never throws for a missing server, pane or tmux.</returns>
    public PaneRead Read(string socket, string pane, CancellationToken cancellationToken);
}
