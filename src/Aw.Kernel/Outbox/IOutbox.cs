namespace Aw.Kernel.Outbox;

/// <summary>
/// The transactional outbox port. Implementations live in a package's Infrastructure (or Aw.Kernel.Data) and
/// store commands in the <see cref="OutboxTables.Commands"/> table.
/// </summary>
public interface IOutbox
{
    /// <summary>Records a command; called inside the unit of work that changes the state.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or AW-KERNEL-003 when the id was already recorded.</returns>
    public ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken);

    /// <summary>Reads up to <paramref name="max"/> commands not yet dispatched, oldest first.</summary>
    /// <param name="max">The most commands to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The pending commands.</returns>
    public ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken);

    /// <summary>Marks a command as sent.</summary>
    /// <param name="id">The command id.</param>
    /// <param name="dispatchedAt">When it was sent.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or AW-KERNEL-002 when there is no such pending command.</returns>
    public ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken);
}
