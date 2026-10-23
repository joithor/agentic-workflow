using Aw.Kernel;

namespace Aw.Evidence.Core.Stack;

/// <summary>Probes the local stack's health.</summary>
public interface IStackProbe
{
    /// <summary>Fetches a page and reads its &lt;title&gt;.</summary>
    /// <param name="url">The page.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>The title text, or AW-EVIDENCE-014.</returns>
    public ValueTask<Result<string>> PageTitleAsync(Uri url, CancellationToken cancellationToken);

    /// <summary>Checks that the database accepts connections.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, or AW-EVIDENCE-014.</returns>
    public ValueTask<Result<Unit>> DatabaseReadyAsync(CancellationToken cancellationToken);

    /// <summary>Checks that a TCP port accepts connections.</summary>
    /// <param name="host">The host name.</param>
    /// <param name="port">The port.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, or AW-EVIDENCE-014.</returns>
    public ValueTask<Result<Unit>> PortOpenAsync(string host, int port, CancellationToken cancellationToken);

    /// <summary>Checks that a database exists on the local server (a worktree slot's own database).</summary>
    /// <param name="database">The database name (letters, digits and underscores only).</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, or AW-EVIDENCE-014 when the server does not answer or has no such database.</returns>
    public ValueTask<Result<Unit>> DatabaseExistsAsync(string database, CancellationToken cancellationToken);

    /// <summary>Finds the directory the process listening on a local port runs from, so a slot served from another checkout is caught.</summary>
    /// <param name="port">The port.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The directory, null when nothing listens on the port or its directory is unknown, or AW-EVIDENCE-014 when the lookup failed.</returns>
    public ValueTask<Result<string?>> ListenerDirectoryAsync(int port, CancellationToken cancellationToken);
}
