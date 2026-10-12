using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The P3 routes (<see cref="SindriApiClient"/>, continued): the queue, an item, its timeline, CreateItem and CloseItem.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<QueueView>> Queue(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<ItemView>> Item(Uri api, string itemId, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TimelineView>> Timeline(Uri api, string itemId, long after, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<ItemCreatedView>> CreateItem(Uri api, CreateItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<CommandAcceptedView>> CloseItem(Uri api, string itemId, CloseItemRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
