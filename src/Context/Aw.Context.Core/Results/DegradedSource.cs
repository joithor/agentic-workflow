using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>A source that didn't answer this query (SND-CONTEXT-010); the query went on without it.</summary>
/// <param name="Source">The source.</param>
/// <param name="Code">Why, as a catalogue code.</param>
/// <param name="Message">One line, never record text.</param>
/// <param name="Since">When it was found unavailable.</param>
public sealed record DegradedSource(SourceId Source, string Code, string Message, DateTimeOffset Since);
