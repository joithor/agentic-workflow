namespace Aw.Kernel.Events;

/// <summary>
/// An event as it is stored and sent (spec §4b.6). <see cref="Id"/> is the idempotency key: consumers process an
/// id once. Build one with <see cref="EventContext.Envelope{TPayload}"/>.
/// </summary>
/// <param name="Id">The event id and idempotency key.</param>
/// <param name="Type">The event type, for example 'item.started'.</param>
/// <param name="SchemaVersion">The envelope schema version.</param>
/// <param name="OccurredAt">When it happened (UTC, written like 'Date#toISOString').</param>
/// <param name="Actor">The acting process.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="Ring">The ring.</param>
/// <param name="CorrelationId">Ties every event of one piece of work together.</param>
/// <param name="CausationId">The event that caused this one, if any.</param>
/// <param name="TraceId">The trace id, if any.</param>
/// <param name="Payload">The event's data.</param>
/// <typeparam name="TPayload">The payload record.</typeparam>
public sealed record EventEnvelope<TPayload>(
    Guid Id,
    string Type,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    string Actor,
    string Tenant,
    int Ring,
    Guid CorrelationId,
    Guid? CausationId,
    string? TraceId,
    TPayload Payload);
