using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The context routes (<see cref="SindriApiClient"/>, continued): the lenses, the file map, the review queue and its decisions, the map status, the node card and the sources.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<LensesView>> Lenses(Uri api, LensesQuery query, CancellationToken cancellationToken) => NotYet<LensesView>(ContextRoutes.Lenses(query));

    /// <inheritdoc />
    public Task<Result<FileMapView>> FileMap(Uri api, FileMapQuery query, CancellationToken cancellationToken) => NotYet<FileMapView>(ContextRoutes.FileMap(query));

    /// <inheritdoc />
    public Task<Result<ReviewQueueView>> ReviewQueue(Uri api, int? limit, CancellationToken cancellationToken) => NotYet<ReviewQueueView>(ContextRoutes.ReviewQueue(limit));

    /// <inheritdoc />
    public Task<Result<ReviewDecisionResponse>> ReviewDecision(Uri api, ReviewDecisionRequest request, CancellationToken cancellationToken) => NotYet<ReviewDecisionResponse>(ContextRoutes.Review);

    /// <inheritdoc />
    public Task<Result<ReviewHistoryView>> ReviewHistory(Uri api, int? limit, CancellationToken cancellationToken) => NotYet<ReviewHistoryView>(ContextRoutes.ReviewHistory(limit));

    /// <inheritdoc />
    public Task<Result<MapStatusView>> MapStatus(Uri api, string? anchor, CancellationToken cancellationToken) => NotYet<MapStatusView>(ContextRoutes.MapStatus(anchor));

    /// <inheritdoc />
    public Task<Result<NodeView>> Node(Uri api, string key, CancellationToken cancellationToken) => NotYet<NodeView>(ContextRoutes.Node(key));

    /// <inheritdoc />
    public Task<Result<SourcesView>> Sources(Uri api, CancellationToken cancellationToken) => NotYet<SourcesView>(ContextRoutes.Sources);

    // Red stub (U9a0): the route is named and nothing is sent until the calls are written.
    private static Task<Result<T>> NotYet<T>(string route) => throw new NotSupportedException(route);
}
