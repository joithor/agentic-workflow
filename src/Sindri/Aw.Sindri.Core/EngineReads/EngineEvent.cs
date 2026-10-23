namespace Aw.Sindri.Core.EngineReads;

/// <summary>One history event, newest first (API spec §3.8, E-30).</summary>
/// <param name="EventId">The Temporal event id.</param>
/// <param name="Time">When it happened.</param>
/// <param name="Type">The event type, as Temporal names it ('ActivityTaskScheduled').</param>
/// <param name="Name">The activity, timer or signal it concerns.</param>
/// <param name="Summary">One line of what happened.</param>
/// <param name="LedgerSequence">The ledger row that mirrors it; null when it isn't mirrored.</param>
public sealed record EngineEvent(long EventId, DateTimeOffset Time, string Type, string? Name, string Summary, long? LedgerSequence);
