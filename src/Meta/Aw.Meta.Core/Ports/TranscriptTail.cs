namespace Aw.Meta.Core.Ports;

/// <summary>The end of a session transcript.</summary>
/// <param name="Path">The transcript file.</param>
/// <param name="ModifiedAt">When it was last written.</param>
/// <param name="Lines">The lines of its last bytes, oldest first; a line cut by the start of the window is dropped.</param>
public sealed record TranscriptTail(string Path, DateTimeOffset ModifiedAt, IReadOnlyList<string> Lines);
