using Aw.Kernel;

namespace Aw.Sindri.Core.Control;

/// <summary>
/// The Temporal dev server 'sindri serve' keeps beside itself (port; engine spec rev 8 §0 "Operations"): one per Sindri
/// home, found by its database file, so a serve that died leaves a server the next start adopts or replaces.
/// </summary>
public interface ITemporalServer
{
    /// <summary>
    /// Adopts the server already running on this home's database when it answers (and replaces one that doesn't), else
    /// starts one on loopback; waits until the 'sindri' namespace answers and registers the search attributes.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The running server, or SND-CLI-002 with the reason it didn't start.</returns>
    public ValueTask<Result<TemporalEndpoint>> Ensure(CancellationToken cancellationToken);

    /// <summary>Stops the server running on this home's database, if one runs, and waits for it to exit.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success, or why it couldn't be stopped.</returns>
    public ValueTask<Result<Unit>> Stop(CancellationToken cancellationToken);

    /// <summary>Reads the doctor's facts: the server on this home's database, whether it answers, its version and its namespace. Read-only.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>The facts.</returns>
    public ValueTask<TemporalFacts> Inspect(CancellationToken cancellationToken);
}
