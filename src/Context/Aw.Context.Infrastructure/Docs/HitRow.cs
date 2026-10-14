namespace Aw.Context.Infrastructure.Docs;

/// <summary>One FTS5 hit: the chunk's document, blob and line span, and the document's title.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Blob">The blob sha the chunk was indexed at.</param>
/// <param name="LineStart">The chunk's first line.</param>
/// <param name="LineEnd">The chunk's last line.</param>
/// <param name="Title">The document's (cleaned) title.</param>
internal sealed record HitRow(string Repo, string Path, string Blob, int LineStart, int LineEnd, string Title);
