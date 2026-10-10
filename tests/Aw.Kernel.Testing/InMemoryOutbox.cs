using Aw.Kernel.Outbox;

namespace Aw.Kernel.Testing;

/// <summary>
/// The in-memory fake of <see cref="IOutbox"/>, with the same delivery state as a real outbox (attempts, next attempt,
/// dead letter) on the clock it is given. Checked by <see cref="OutboxContract"/>.
/// </summary>
/// <param name="clock">Where 'now' comes from (a 'FakeTimeProvider' in tests).</param>
/// <param name="policy">When a failed command is tried again.</param>
public sealed class InMemoryOutbox(TimeProvider clock, OutboxRetryPolicy policy) : IOutbox
{
    /// <inheritdoc />
    public ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: InMemoryOutbox.Enqueue ({clock}, {policy})");

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: InMemoryOutbox.ReadPending");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: InMemoryOutbox.MarkDispatched");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: InMemoryOutbox.MarkFailed");
}
