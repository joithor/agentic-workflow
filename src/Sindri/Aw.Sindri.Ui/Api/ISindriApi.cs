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

    /// <summary>GET /sindri/commands: the command catalog (read on connect and on a new epoch, UI spec §9.1).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The catalog, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<CommandList>> Commands(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/services (API P2): U1 reads its EmptyText sentence and asOf for the stalled and rebuilding banners.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ServiceList>> Services(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/views/rebuild (RebuildViews, desk, API P2): sent once, never retried by the app (UI spec §9.1).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, the API's refusal (title and detail as served), or SND-UI-002/003.</returns>
    public Task<Result<CommandAcceptedView>> RebuildViews(Uri api, CancellationToken cancellationToken);
}
