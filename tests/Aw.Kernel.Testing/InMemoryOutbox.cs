using Aw.Kernel.Outbox;

namespace Aw.Kernel.Testing;

/// <summary>
/// The in-memory fake of <see cref="IOutbox"/>, with the same delivery state as a real outbox (attempts, next attempt,
/// dead letter) on the clock it is given. Used directly, its writes apply at once; <see cref="In"/> gives the view a use
/// case uses, whose writes join an <see cref="InMemoryUnitOfWork"/> and land with its commit. Times are kept in UTC.
/// Checked by <see cref="OutboxContract"/>.
/// </summary>
/// <param name="clock">Where 'now' comes from (a 'FakeTimeProvider' in tests).</param>
/// <param name="policy">When a failed command is tried again.</param>
public sealed class InMemoryOutbox(TimeProvider clock, OutboxRetryPolicy policy) : IOutbox
{
    private readonly List<Entry> _entries = [];

    /// <summary>Every recorded command with its delivery state, in enqueue order, for assertions.</summary>
    public IReadOnlyList<OutboxEntry> Entries =>
        [.. _entries.Select(e => new OutboxEntry(e.Command, e.Attempts, e.LastError, e.NextAttemptAt, e.DispatchedAt, e.DeadLetteredAt))];

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken) => Done(EnqueueNow(command));

    /// <summary>
    /// A view of this outbox whose writes join <paramref name="work"/>: each write is checked now and applied when the
    /// unit commits, so a disposed unit leaves no command behind. Reads see committed state only.
    /// </summary>
    /// <param name="work">The use case's unit of work.</param>
    /// <returns>The view.</returns>
    public IOutbox In(InMemoryUnitOfWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return new UnitView(this, work);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken)
    {
        if (max < 1)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<OutboxCommand>>>(KernelErrors.Invalid.With("ReadPending needs max >= 1"));
        }

        var now = clock.GetUtcNow();
        IReadOnlyList<OutboxCommand> due =
        [
            .. _entries
                .Where(e => e.IsPending && (e.NextAttemptAt is null || e.NextAttemptAt <= now))
                .OrderBy(e => e.Command.EnqueuedAt)
                .Take(max)
                .Select(e => e.Command),
        ];
        return ValueTask.FromResult(Results.Success(due));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken) =>
        Done(MarkDispatchedNow(id, dispatchedAt));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken) => Done(MarkFailedNow(id, error));

    private static ValueTask<Result<Unit>> Done(Result<Unit> result) => ValueTask.FromResult(result);

    private Result<Unit> EnqueueNow(OutboxCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_entries.Exists(e => e.Command.Id == command.Id))
        {
            return KernelErrors.Conflict.With($"outbox command {command.Id} is already recorded");
        }

        _entries.Add(new Entry(command with { EnqueuedAt = command.EnqueuedAt.ToUniversalTime() }));
        return Results.Ok();
    }

    private Result<Unit> MarkDispatchedNow(Guid id, DateTimeOffset dispatchedAt)
    {
        var entry = Pending(id);
        if (entry is null)
        {
            return NoSuchPending(id);
        }

        entry.DispatchedAt = dispatchedAt.ToUniversalTime();
        return Results.Ok();
    }

    private Result<Unit> MarkFailedNow(Guid id, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var entry = Pending(id);
        if (entry is null)
        {
            return NoSuchPending(id);
        }

        var now = clock.GetUtcNow().ToUniversalTime();
        entry.Attempts++;
        entry.LastError = error;
        entry.NextAttemptAt = policy.NextAttempt(entry.Attempts, now);
        entry.DeadLetteredAt = entry.NextAttemptAt is null ? now : null;
        return Results.Ok();
    }

    private static Error NoSuchPending(Guid id) => KernelErrors.NotFound.With($"no pending outbox command {id}");

    private Entry? Pending(Guid id) => _entries.Find(e => e.Command.Id == id && e.IsPending);

    private sealed class UnitView(InMemoryOutbox outbox, InMemoryUnitOfWork work) : IOutbox
    {
        public ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (outbox._entries.Exists(e => e.Command.Id == command.Id))
            {
                return Done(KernelErrors.Conflict.With($"outbox command {command.Id} is already recorded"));
            }

            work.Enlist(() => _ = outbox.EnqueueNow(command));
            return Done(Results.Ok());
        }

        public ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken) =>
            outbox.ReadPending(max, cancellationToken);

        public ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken) =>
            Deferred(id, () => _ = outbox.MarkDispatchedNow(id, dispatchedAt));

        public ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken) =>
            Deferred(id, () => _ = outbox.MarkFailedNow(id, error));

        private ValueTask<Result<Unit>> Deferred(Guid id, Action apply)
        {
            if (outbox.Pending(id) is null)
            {
                return Done(NoSuchPending(id));
            }

            work.Enlist(apply);
            return Done(Results.Ok());
        }
    }

    private sealed class Entry(OutboxCommand command)
    {
        public OutboxCommand Command { get; } = command;

        public int Attempts { get; set; }

        public Error? LastError { get; set; }

        public DateTimeOffset? NextAttemptAt { get; set; }

        public DateTimeOffset? DispatchedAt { get; set; }

        public DateTimeOffset? DeadLetteredAt { get; set; }

        public bool IsPending => DispatchedAt is null && DeadLetteredAt is null;
    }
}
