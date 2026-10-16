using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>The P4 routes (<see cref="SindriApiClient"/>, continued): what needs the human, the decisions, the requests, Decide and FileRequest.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<NeedsYouList>> NeedsYou(Uri api, CancellationToken cancellationToken) =>
        Get<NeedsYouList>(api, NeedsYouRoutes.NeedsYou, cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<DecisionList>> Decisions(Uri api, bool? pending, string? item, CancellationToken cancellationToken) =>
        Get<DecisionList>(api, NeedsYouRoutes.Decisions(pending, item), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<RequestList>> Requests(Uri api, string? state, string? item, CancellationToken cancellationToken) =>
        Get<RequestList>(api, NeedsYouRoutes.RequestList(state, item), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<CommandAcceptedView>> Decide(Uri api, string decisionId, DecideRequest request, CancellationToken cancellationToken) =>
        Call<CommandAcceptedView>(api, HttpMethod.Post, NeedsYouRoutes.Decide(decisionId), cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<RequestFiledView>> FileRequest(Uri api, FileRequestRequest request, CancellationToken cancellationToken) =>
        Call<RequestFiledView>(api, HttpMethod.Post, NeedsYouRoutes.Requests, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));
}
