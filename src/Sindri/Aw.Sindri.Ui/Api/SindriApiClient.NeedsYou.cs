using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The P4 routes (<see cref="SindriApiClient"/>, continued): what needs the human, the decisions, the requests, Decide and FileRequest.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<NeedsYouList>> NeedsYou(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<DecisionList>> Decisions(Uri api, bool? pending, string? item, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<RequestList>> Requests(Uri api, string? state, string? item, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<CommandAcceptedView>> Decide(Uri api, string decisionId, DecideRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<RequestFiledView>> FileRequest(Uri api, FileRequestRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
