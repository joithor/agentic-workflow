using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

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

    /// <summary>GET /sindri/workflow/instances (API §3.8, E-20): the instance rows and the root-wide counts, narrowed by the query.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">What to ask for: root, open, recipe, subject or one address.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<InstanceList>> Instances(Uri api, InstanceQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow/instances/{id} (API §3.8, Q-API-2): one instance by its opaque id. An address is never a path segment: resolve it with <see cref="Instances"/>.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The instance id (WFI-…) as served.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The instance, the API's refusal (SND-ITEM-404 and the like), or SND-UI-002/003.</returns>
    public Task<Result<InstanceDetail>> Instance(Uri api, string id, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow/instances/{id}/facts/{key}: the body behind one fact row, capped by the API (truncated says so).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The instance id.</param>
    /// <param name="key">The fact row's key in its fact: form.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The fact, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<FactView>> InstanceFact(Uri api, string id, string key, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow/recipes/{id}[@{hash}] (API §3.8, E-22): what a recipe declares.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="recipe">The recipe id.</param>
    /// <param name="hash">The approved hash, or null for the approved one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The recipe, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<RecipeView>> Recipe(Uri api, string recipe, string? hash, CancellationToken cancellationToken);

    // LANE ANCHOR U3': the Needs you, decision and request reads, one method each. Insert immediately above this marker, never between the markers.
    // LANE ANCHOR U8: the command sends (nonce, commands, complete), once API P11 serves their shapes (Q-API-7/8); none is declared before. Insert immediately below this marker, never between the markers.
}
