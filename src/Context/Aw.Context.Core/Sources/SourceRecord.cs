namespace Aw.Context.Core.Sources;

/// <summary>A fetched record, raw: the engine sanitizes, scrubs and trust-tags every field once (spec §5.1, §7).</summary>
/// <param name="Ref">The record.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its text (at most 64 KB; the engine cuts the rest).</param>
/// <param name="Author">Its author, if the source has one.</param>
/// <param name="CreatedAt">When it was created, if known.</param>
/// <param name="UpdatedAt">When it last changed, if known.</param>
public sealed record SourceRecord(SourceRef Ref, RecordKind Kind, string Title, string Text, string? Author, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt);
