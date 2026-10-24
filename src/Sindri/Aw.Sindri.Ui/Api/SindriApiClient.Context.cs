using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The context routes (<see cref="SindriApiClient"/>, continued): the lenses, the file map, the review queue and its decisions, the map status, the node card and the sources. Reads of the context database never carry X-Sindri-After.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<LensesView>> Lenses(Uri api, LensesQuery query, CancellationToken cancellationToken) =>
        Get<LensesView>(api, ContextRoutes.Lenses(query), cancellationToken);

    /// <inheritdoc />
    public Task<Result<CodeView>> Code(Uri api, CodeQuery query, CancellationToken cancellationToken) =>
        Get<CodeView>(api, ContextRoutes.Code(query), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ItemScopeView>> ItemScope(Uri api, string itemId, CancellationToken cancellationToken) =>
        Get<ItemScopeView>(api, "/sindri/items/" + Uri.EscapeDataString(itemId) + "/scope", cancellationToken);

    /// <inheritdoc />
    public Task<Result<WholeMapView>> WholeMap(Uri api, WholeMapQuery query, CancellationToken cancellationToken) =>
        Get<WholeMapView>(api, ContextRoutes.WholeMap(query), cancellationToken);

    /// <inheritdoc />
    public Task<Result<FileMapView>> FileMap(Uri api, FileMapQuery query, CancellationToken cancellationToken) =>
        Get<FileMapView>(api, ContextRoutes.FileMap(query), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ReviewQueueView>> ReviewQueue(Uri api, int? limit, CancellationToken cancellationToken) =>
        Get<ReviewQueueView>(api, ContextRoutes.ReviewQueue(limit), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ReviewDecisionResponse>> ReviewDecision(Uri api, ReviewDecisionRequest request, CancellationToken cancellationToken) =>
        Call<ReviewDecisionResponse>(api, HttpMethod.Post, ContextRoutes.Review, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<ReviewHistoryView>> ReviewHistory(Uri api, int? limit, CancellationToken cancellationToken) =>
        Get<ReviewHistoryView>(api, ContextRoutes.ReviewHistory(limit), cancellationToken);

    /// <inheritdoc />
    public Task<Result<MapStatusView>> MapStatus(Uri api, string? anchor, CancellationToken cancellationToken) =>
        Get<MapStatusView>(api, ContextRoutes.MapStatus(anchor), cancellationToken);

    /// <inheritdoc />
    public Task<Result<MapPublishView>> MapPublish(Uri api, CancellationToken cancellationToken) =>
        Get<MapPublishView>(api, ContextRoutes.MapPublish, cancellationToken);

    /// <inheritdoc />
    public Task<Result<NodeView>> Node(Uri api, string key, CancellationToken cancellationToken) =>
        Get<NodeView>(api, ContextRoutes.Node(key), cancellationToken);

    /// <inheritdoc />
    public Task<Result<SourcesView>> Sources(Uri api, CancellationToken cancellationToken) =>
        Get<SourcesView>(api, ContextRoutes.Sources, cancellationToken);

    /// <inheritdoc />
    public Task<Result<QueryAnchorsView>> QueryAnchors(Uri api, string text, CancellationToken cancellationToken) =>
        Get<QueryAnchorsView>(api, ContextRoutes.Query(text), cancellationToken);
}
