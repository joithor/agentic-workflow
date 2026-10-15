using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>Sindri's HTTP API as the app uses it (docs/csharp/sindri.md). Every call names the base URL from control.json.</summary>
internal interface ISindriApi
{
    /// <summary>GET /sindri/ping.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answering process, or SND-UI-002/003.</returns>
    public Task<Result<PingView>> Ping(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/status.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status, or SND-UI-002/003.</returns>
    public Task<Result<StatusView>> Status(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/events?after=N: the ledger's events after a cursor, as they happen. Ends when the connection does.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="after">The last sequence already seen (0 for all).</param>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>The events, in order.</returns>
    public IAsyncEnumerable<EventView> Events(Uri api, long after, CancellationToken cancellationToken);
}
