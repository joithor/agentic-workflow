namespace Aw.Kernel.Outbox;

/// <summary>
/// The transactional outbox port. Implementations live in a package's Infrastructure (or Aw.Kernel.Data) and
/// store commands in the <see cref="OutboxTables.Commands"/> table, with per-command delivery state: the attempt
/// count, the last error, the next attempt time and, once the retry policy gives up, the dead-letter time.
/// Every implementation passes Aw.Kernel.Testing's OutboxContract; Aw.Kernel.Data ships the SQLite one (SqliteOutbox).
/// </summary>
public interface IOutbox
{
    /// <summary>Records a command; called inside the unit of work that changes the state.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or AW-KERNEL-003 when the id was already recorded.</returns>
    public ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Reads up to <paramref name="max"/> commands that are due, oldest first: not dispatched, not dead-lettered,
    /// and with no next attempt time or one that has passed on the implementation's clock.
    /// </summary>
    /// <param name="max">The most commands to return, at least 1.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The due commands, or AW-KERNEL-001 when <paramref name="max"/> is below 1.</returns>
    public ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken);

    /// <summary>Marks a command as sent.</summary>
    /// <param name="id">The command id.</param>
    /// <param name="dispatchedAt">When it was sent.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or AW-KERNEL-002 when there is no such pending command.</returns>
    public ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Records a failed send: the implementation adds one to the attempt count, stores <paramref name="error"/>,
    /// and sets the next attempt with its <see cref="OutboxRetryPolicy"/>. When the policy returns null the command is
    /// dead-lettered, so a poison command never blocks the ones behind it.
    /// </summary>
    /// <param name="id">The command id.</param>
    /// <param name="error">Why the send failed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or AW-KERNEL-002 when there is no such pending command.</returns>
    public ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken);
}
