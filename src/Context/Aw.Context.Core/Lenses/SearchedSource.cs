namespace Aw.Context.Core.Lenses;

/// <summary>One source a lenses answer read, or couldn't (scoping spec C6). Three separate fields, so a reader never parses one for the others.</summary>
/// <param name="Id">The source: map, onyx or code-index.</param>
/// <param name="State">ok, not-configured, unavailable or error.</param>
/// <param name="Reason">A recorded-limit word ('index-not-built', 'not-configured', 'not-synced'), or null.</param>
/// <param name="Code">An SND-CONTEXT-* code, or null.</param>
/// <param name="AsOf">How current what was read is (the map version's creation; an intent source's last sync), or null.</param>
public sealed record SearchedSource(Sources.SourceId Id, SearchedState State, string? Reason, string? Code, DateTimeOffset? AsOf);
