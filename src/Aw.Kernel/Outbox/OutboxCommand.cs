namespace Aw.Kernel.Outbox;

/// <summary>
/// An outbound side effect (a GitHub comment, a message) recorded in the same transaction as the state change
/// that caused it, and sent later by the dispatcher (spec §4b.6). It carries the same who-and-why seams as an
/// <see cref="Events.EventEnvelope{TPayload}"/>, so the dispatcher can re-run the gate checks for the right tenant
/// and ring when it sends it (spec §4e, B12). Build one with <see cref="Events.EventContext.Command"/>, which takes
/// the actor from the host-built context.
/// </summary>
/// <param name="Id">The command id and idempotency key.</param>
/// <param name="Kind">What to do, for example 'github.comment'.</param>
/// <param name="PayloadJson">The command's data as JSON.</param>
/// <param name="SchemaVersion">The command schema version.</param>
/// <param name="EnqueuedAt">When it was recorded.</param>
/// <param name="Actor">The process that recorded it (set by the host).</param>
/// <param name="Tenant">The tenant it belongs to.</param>
/// <param name="Ring">The ring it was recorded in.</param>
/// <param name="CorrelationId">The work it belongs to.</param>
/// <param name="CausationId">The event that caused it, if any.</param>
/// <param name="TraceId">The trace id, if any.</param>
public sealed record OutboxCommand(
    Guid Id,
    string Kind,
    string PayloadJson,
    int SchemaVersion,
    DateTimeOffset EnqueuedAt,
    string Actor,
    string Tenant,
    int Ring,
    Guid CorrelationId,
    Guid? CausationId,
    string? TraceId)
{
    /// <summary>The command schema version this build writes.</summary>
    public const int CurrentSchemaVersion = 1;
}
