namespace Aw.Context.Infrastructure.Docs;

/// <summary>One chunk of a document: its line span (from 1, inclusive) and text.</summary>
/// <param name="LineStart">Its first line.</param>
/// <param name="LineEnd">Its last line.</param>
/// <param name="Text">Its text.</param>
internal sealed record DocChunk(int LineStart, int LineEnd, string Text);
