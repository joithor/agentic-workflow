namespace Aw.Sindri.Core.EngineReads;

/// <summary>One history event as the adapter read it.</summary>
/// <param name="EventId">The Temporal event id.</param>
/// <param name="Time">When it happened.</param>
/// <param name="Type">The event type.</param>
/// <param name="Name">The activity, timer or signal it concerns.</param>
/// <param name="Summary">One line of what happened.</param>
/// <param name="Activity">The activity id of a RecordEvent marker, which the ledger row records as its Temporal event id; null for any other event.</param>
public sealed record EngineEventFacts(long EventId, DateTimeOffset Time, string Type, string? Name, string Summary, string? Activity);
