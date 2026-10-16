using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>What the last sync of an intent source did, as 'context sources status' shows it (spec §8.8, S-5B). Counts and a code only.</summary>
/// <param name="LastSync">When the last successful page was committed, or null when it never ran.</param>
/// <param name="State">Ok after a good sync, Degraded when it never ran, Unavailable when the last run failed.</param>
/// <param name="Records">Records stored for the source.</param>
/// <param name="Linked">Links stored for them.</param>
/// <param name="Decisions">Decisions extracted from them.</param>
/// <param name="Queued">Records waiting for decision extraction.</param>
/// <param name="Code">The last run's code, or null: a failure (State is Unavailable), SND-CONTEXT-059 when the time cap stopped it (partial, State unchanged), or SND-CONTEXT-042 while records wait for extraction (State unchanged).</param>
public sealed record SourceSyncStatus(DateTimeOffset? LastSync, SourceState State, int Records, int Linked, int Decisions, int Queued, string? Code);
