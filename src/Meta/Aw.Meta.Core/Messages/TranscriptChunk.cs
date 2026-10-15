namespace Aw.Meta.Core.Messages;

/// <summary>Complete lines read from a transcript.</summary>
/// <param name="Lines">The lines, without line endings.</param>
/// <param name="NextOffset">The byte offset just after the last complete line; where the next read starts.</param>
public sealed record TranscriptChunk(IReadOnlyList<string> Lines, long NextOffset);
