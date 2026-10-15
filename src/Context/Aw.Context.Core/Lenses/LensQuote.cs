using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Lenses;

/// <summary>The stored quote a decision rests on (always untrusted: it is record text).</summary>
/// <param name="Record">The record it came from.</param>
/// <param name="Text">The quote as the caller reads it.</param>
/// <param name="Trust">Always untrusted.</param>
public sealed record LensQuote(RecordKey Record, string Text, Results.Trust Trust);
