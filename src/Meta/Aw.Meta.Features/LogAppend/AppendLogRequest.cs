namespace Aw.Meta.Features.LogAppend;

/// <summary>The AppendLog request: add a line to the running log.</summary>
/// <param name="Kind">merge, decision, lanes, incident or note.</param>
/// <param name="Text">The text.</param>
public sealed record AppendLogRequest(string Kind, string Text);
