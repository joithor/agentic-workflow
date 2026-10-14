namespace Aw.Context.Core.Trust;

/// <summary>A scrubbed field.</summary>
/// <param name="Text">The text to write: the input with secrets replaced, or the blocked placeholder.</param>
/// <param name="Redacted">True when at least one secret was replaced.</param>
/// <param name="Blocked">True when the whole field was withheld.</param>
public sealed record ScrubbedField(string Text, bool Redacted, bool Blocked);
