using Aw.Kernel.Outbox;

namespace Aw.Kernel.Testing;

/// <summary>A command in <see cref="InMemoryOutbox"/> with its delivery state, for assertions.</summary>
/// <param name="Command">The command as recorded.</param>
/// <param name="Attempts">Failed sends so far.</param>
/// <param name="LastError">Why the last send failed, if one did.</param>
/// <param name="NextAttemptAt">When it is due again after a failure, if it is waiting.</param>
/// <param name="DispatchedAt">When it was sent, if it was.</param>
/// <param name="DeadLetteredAt">When the retry policy gave up, if it did.</param>
public sealed record OutboxEntry(
    OutboxCommand Command,
    int Attempts,
    Error? LastError,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? DeadLetteredAt);
