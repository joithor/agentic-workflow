namespace Aw.Meta.Features.LogAppend;

/// <summary>The AppendLog response.</summary>
/// <param name="Line">The line that was appended.</param>
public sealed record AppendLogResponse(string Line);
