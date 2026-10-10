namespace Aw.Kernel.Outbox;

/// <summary>
/// An outbound side effect (a GitHub comment, a message) recorded in the same transaction as the state change
/// that caused it, and sent later by the dispatcher (spec §4b.6). The dispatcher re-runs the gate checks on each
/// command when it sends it (spec §4e, B12).
/// </summary>
/// <param name="Id">The command id and idempotency key.</param>
/// <param name="Kind">What to do, for example 'github.comment'.</param>
/// <param name="PayloadJson">The command's data as JSON.</param>
/// <param name="EnqueuedAt">When it was recorded.</param>
/// <param name="Actor">The process that recorded it (set by the host).</param>
/// <param name="CorrelationId">The work it belongs to.</param>
public sealed record OutboxCommand(
    Guid Id,
    string Kind,
    string PayloadJson,
    DateTimeOffset EnqueuedAt,
    string Actor,
    Guid CorrelationId);
