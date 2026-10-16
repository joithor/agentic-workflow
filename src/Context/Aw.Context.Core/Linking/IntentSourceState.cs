namespace Aw.Context.Core.Linking;

/// <summary>What the store knows of one intent source's sync (counts and codes only, spec §7).</summary>
/// <param name="Cursor">The stored cursor, or null before the first page.</param>
/// <param name="LastSync">When the last page committed, or null.</param>
/// <param name="LastError">The code the last run recorded, or null: a failure, SND-CONTEXT-059 when the time cap stopped it (partial, not a failure) or SND-CONTEXT-042 when extraction was held back; cleared by the next committed page.</param>
/// <param name="Records">Records stored for the source.</param>
/// <param name="Links">Links stored for them.</param>
/// <param name="Decisions">Decisions extracted from them.</param>
/// <param name="Queued">Records waiting for extraction.</param>
public sealed record IntentSourceState(string? Cursor, DateTimeOffset? LastSync, string? LastError, int Records, int Links, int Decisions, int Queued);
