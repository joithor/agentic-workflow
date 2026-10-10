using Aw.Kernel.Outbox;

namespace Aw.Kernel.Testing;

/// <summary>
/// The in-memory fake of <see cref="IOutbox"/>, with the same delivery state as a real outbox (attempts, next attempt,
/// dead letter) on the clock it is given. Writes apply at once (it doesn't enlist in a unit of work). Checked by
/// <see cref="OutboxContract"/>.
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
    public ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_entries.Exists(e => e.Command.Id == command.Id))
        {
            return Done(KernelErrors.Conflict.With($"outbox command {command.Id} is already recorded"));
        }

        _entries.Add(new Entry(command));
        return Done(Results.Ok());
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
    public ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken)
    {
        var entry = Pending(id);
        if (entry is null)
        {
            return Done(NoSuchPending(id));
        }

        entry.DispatchedAt = dispatchedAt;
        return Done(Results.Ok());
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        var entry = Pending(id);
        if (entry is null)
        {
            return Done(NoSuchPending(id));
        }

        var now = clock.GetUtcNow();
        entry.Attempts++;
        entry.LastError = error;
        entry.NextAttemptAt = policy.NextAttempt(entry.Attempts, now);
        entry.DeadLetteredAt = entry.NextAttemptAt is null ? now : null;
        return Done(Results.Ok());
    }

    private static ValueTask<Result<Unit>> Done(Result<Unit> result) => ValueTask.FromResult(result);

    private static Error NoSuchPending(Guid id) => KernelErrors.NotFound.With($"no pending outbox command {id}");

    private Entry? Pending(Guid id) => _entries.Find(e => e.Command.Id == id && e.IsPending);

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
