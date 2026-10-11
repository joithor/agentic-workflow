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
}
