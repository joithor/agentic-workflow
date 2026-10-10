using Aw.Kernel.Outbox;

namespace Aw.Kernel.Data;

/// <summary>
/// The SQLite <see cref="IOutbox"/> over the <see cref="SqliteOutboxSchema"/> table. It runs on the session it is
/// given, so commands enqueued through a <see cref="SqliteUnitOfWork"/> commit or roll back with the state change that
/// recorded them (the transactional outbox, spec §4b.6). Time comes from <paramref name="clock"/>; failures are retried
/// by <paramref name="policy"/>. Checked by the shared OutboxContract (Aw.Kernel.Testing).
/// </summary>
/// <param name="session">The session (normally the use case's unit of work).</param>
/// <param name="clock">Where 'now' comes from.</param>
/// <param name="policy">When a failed command is tried again.</param>
public sealed class SqliteOutbox(ISqliteSession session, TimeProvider clock, OutboxRetryPolicy policy) : IOutbox
{
    /// <inheritdoc />
    public ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: SqliteOutbox.Enqueue ({session}, {clock}, {policy})");

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: SqliteOutbox.ReadPending");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: SqliteOutbox.MarkDispatched");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: SqliteOutbox.MarkFailed");
}
