using Aw.Context.Core.Linking;

namespace Aw.Context.Features.SyncSources;

/// <summary>A record committed in a sync page and waiting for decision extraction, with its cleaned text (the page stores the text too, for relinking against a later map).</summary>
/// <param name="Record">The record as stored.</param>
/// <param name="Text">Its cleaned text (scrubbed at the ingest boundary).</param>
/// <param name="Links">The links its rules found.</param>
internal sealed record PendingRecord(IntentRecord Record, string Text, IReadOnlyList<IntentLink> Links);
