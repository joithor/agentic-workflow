namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// Splits a markdown document into chunks for the FTS5 index: a new chunk at every heading, and at most
/// <see cref="MaxLines"/> lines or <see cref="MaxChars"/> characters per chunk; blank-only chunks are dropped. Lines
/// are numbered from 1, so a chunk's span cites the blob's own lines.
/// </summary>
internal static class DocChunker
{
    internal const int MaxLines = 40;

    /// <summary>The largest line number a chunk id carries ('#L&lt;a&gt;-L&lt;b&gt;' takes up to 7 digits).</summary>
    internal const int MaxLine = 9_999_999;
    internal const int MaxChars = 2_000;

    internal static IReadOnlyList<DocChunk> Split(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var chunks = new List<DocChunk>();
        var start = 0;
        var size = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var heading = lines[i].StartsWith('#') && i > start;
            if (heading || i - start >= MaxLines || (size + lines[i].Length > MaxChars && i > start))
            {
                Add(chunks, lines, start, i);
                start = i;
                size = 0;
            }

            size += lines[i].Length + 1;
        }

        Add(chunks, lines, start, lines.Length);
        return chunks;
    }

    /// <summary>The document's title: its first '# ' heading, else its file name.</summary>
    /// <param name="text">The document.</param>
    /// <param name="path">Its path.</param>
    /// <returns>The title (raw: the ingest filter cleans it).</returns>
    internal static string Title(string text, string path) =>
        text.Split('\n').Select(l => l.TrimEnd('\r')).FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal)) is { } heading
            ? heading[2..].Trim()
            : Path.GetFileName(path);

    private static void Add(List<DocChunk> chunks, string[] lines, int start, int end)
    {
        var text = string.Join('\n', lines[start..end]);
        if (!string.IsNullOrWhiteSpace(text))
        {
            chunks.Add(new DocChunk(start + 1, end, text));
        }
    }
}
