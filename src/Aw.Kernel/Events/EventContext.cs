using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Kernel.Outbox;

namespace Aw.Kernel.Events;

/// <summary>
/// Who an event is for and why (spec §4b.6 seams, §17.20–21 of the factory addendum): the actor, tenant and ring,
/// and the correlation, causation and trace that tie events together. The Host builds it; callers never set
/// <see cref="Actor"/> themselves (spec §4e, B12).
/// </summary>
/// <remarks>
/// <see cref="Create"/> is public because Kernel can't name the host projects that may call it. Only Host projects
/// call it: that rule is enforced by the in-repo analyzer (spec §4b.6, Aw.Analyzers) and the architecture test that
/// checks Host-only registration, not by this type. Everything else receives a context and derives from it with
/// <see cref="CausedBy{TPayload}"/>.
/// </remarks>
public sealed record EventContext
{
    private EventContext(string actor, string tenant, int ring, Guid correlationId, Guid? causationId, string? traceId)
    {
        Actor = actor;
        Tenant = tenant;
        Ring = ring;
        CorrelationId = correlationId;
        CausationId = causationId;
        TraceId = traceId;
    }

    /// <summary>The highest ring number (rings 0, 1 and 2).</summary>
    public const int MaxRing = 2;

    private const int MaxTraceIdLength = 128;

    /// <summary>The process acting, for example 'host:sindri'. Set by the host process.</summary>
    public string Actor { get; }

    /// <summary>The tenant the event belongs to.</summary>
    public string Tenant { get; }

    /// <summary>The ring, 0 to <see cref="MaxRing"/>.</summary>
    public int Ring { get; }

    /// <summary>Ties every event of one piece of work together.</summary>
    public Guid CorrelationId { get; }

    /// <summary>The event that caused this one, if any.</summary>
    public Guid? CausationId { get; }

    /// <summary>The trace id, when tracing is on.</summary>
    public string? TraceId { get; }

    /// <summary>Builds a context, checking that it is complete.</summary>
    /// <param name="actor">The acting process.</param>
    /// <param name="tenant">The tenant.</param>
    /// <param name="ring">The ring, 0 to <see cref="MaxRing"/>.</param>
    /// <param name="correlationId">The correlation id.</param>
    /// <param name="traceId">The trace id, if any.</param>
    /// <returns>The context, or AW-KERNEL-001 when a field is missing or out of range.</returns>
    public static Result<EventContext> Create(string actor, string tenant, int ring, Guid correlationId, string? traceId = null)
    {
        var problem = Problem(actor, tenant, ring, correlationId, traceId);
        return problem is null
            ? new EventContext(actor, tenant, ring, correlationId, causationId: null, traceId)
            : KernelErrors.Invalid.With(problem);
    }

    private static string? Problem(string actor, string tenant, int ring, Guid correlationId, string? traceId) => true switch
    {
        _ when string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(tenant) => "An event context needs an actor and a tenant.",
        _ when ring is < 0 or > MaxRing => string.Create(CultureInfo.InvariantCulture, $"Ring {ring} is outside 0..{MaxRing}."),
        _ when correlationId == Guid.Empty => "An event context needs a correlation id (Guid.Empty is not one).",
        _ when traceId is not null && !IsTraceId(traceId) => "A trace id is 1 to 128 printable ASCII characters with no spaces.",
        _ => null,
    };

    private static bool IsTraceId(string traceId) =>
        traceId.Length is > 0 and <= MaxTraceIdLength && traceId.All(c => c is > ' ' and <= '~');

    /// <summary>The context for an event caused by <paramref name="cause"/>: same correlation, causation set.</summary>
    /// <param name="cause">The causing event.</param>
    /// <typeparam name="TPayload">The causing event's payload.</typeparam>
    /// <returns>The derived context.</returns>
    public EventContext CausedBy<TPayload>(EventEnvelope<TPayload> cause) =>
        new(Actor, Tenant, Ring, cause.CorrelationId, cause.Id, TraceId);

    /// <summary>Records an outbound command for this context: new id, current time, and this context's seams.</summary>
    /// <param name="kind">What to do, for example 'github.comment'.</param>
    /// <param name="payloadJson">The command's data as JSON.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="ids">The id source.</param>
    /// <returns>The command, ready for <see cref="IOutbox.Enqueue"/>.</returns>
    public OutboxCommand Command(string kind, string payloadJson, TimeProvider clock, IIdGenerator ids) =>
        new(ids.NewId(), kind, payloadJson, OutboxCommand.CurrentSchemaVersion, clock.GetUtcNow(), Actor, Tenant, Ring,
            CorrelationId, CausationId, TraceId);

    /// <summary>Wraps <paramref name="payload"/> in an envelope with a new id and the current time.</summary>
    /// <param name="type">The event type, for example 'item.started'.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="ids">The id source.</param>
    /// <typeparam name="TPayload">The payload record.</typeparam>
    /// <returns>The envelope.</returns>
    public EventEnvelope<TPayload> Envelope<TPayload>(string type, TPayload payload, TimeProvider clock, IIdGenerator ids) =>
        new(ids.NewId(), type, EventSchema.CurrentVersion, clock.GetUtcNow(), Actor, Tenant, Ring,
            CorrelationId, CausationId, TraceId, payload);
}
