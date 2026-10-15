using System.Text;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>
/// The ITranscriptReader contract: the same tests run against the in-memory fake and every real adapter
/// (.agents/rules/csharp.md, Seams). A derived class supplies the reader and a way to write transcripts it reads.
/// </summary>
public abstract class TranscriptReaderContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A reader over the transcripts <see cref="Write"/> and <see cref="Append"/> create.</summary>
    /// <returns>The reader.</returns>
    protected abstract ITranscriptReader Create();

    /// <summary>Creates or replaces a transcript.</summary>
    /// <param name="name">A name unique to the test.</param>
    /// <param name="content">The whole text.</param>
    /// <returns>The path to read it by.</returns>
    protected abstract string Write(string name, string content);

    /// <summary>Appends to a transcript made by <see cref="Write"/>.</summary>
    /// <param name="path">The path <see cref="Write"/> returned.</param>
    /// <param name="content">The text to add.</param>
    protected abstract void Append(string path, string content);

    /// <summary>Complete lines are returned and the offset moves past them.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Complete_lines_are_read_and_the_offset_moves_past_them()
    {
        var reader = Create();
        var path = Write("a", "{\"a\":1}\n{\"b\":2}\n");

        var chunk = await reader.ReadFrom(path, 0, _none);

        Expect.That(chunk.IsSuccess && chunk.Value.Lines.SequenceEqual(["{\"a\":1}", "{\"b\":2}"], StringComparer.Ordinal), "both lines");
        Expect.That(chunk.Value.NextOffset == Encoding.UTF8.GetByteCount("{\"a\":1}\n{\"b\":2}\n"), "the offset is the byte length");
    }

    /// <summary>A partial trailing line is left unread until it is complete.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_partial_trailing_line_is_not_consumed()
    {
        var reader = Create();
        var path = Write("b", "one\ntwo-partial");

        var first = await reader.ReadFrom(path, 0, _none);
        Append(path, " done\n");
        var second = await reader.ReadFrom(path, first.Value.NextOffset, _none);

        Expect.That(first.Value.Lines.SequenceEqual(["one"], StringComparer.Ordinal) && first.Value.NextOffset == 4, "stops before the partial line");
        Expect.That(second.Value.Lines.SequenceEqual(["two-partial done"], StringComparer.Ordinal), "the completed line is read whole");
    }

    /// <summary>Offsets count bytes, so multi-byte text is not split or skipped.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Offsets_count_bytes_not_characters()
    {
        var reader = Create();
        var path = Write("c", "héllo ✓\nnext\n");

        var first = await reader.ReadFrom(path, 0, _none);
        var rest = await reader.ReadFrom(path, Encoding.UTF8.GetByteCount("héllo ✓\n"), _none);

        Expect.That(first.Value.Lines[0] == "héllo ✓", "multi-byte text reads back");
        Expect.That(rest.Value.Lines.SequenceEqual(["next"], StringComparer.Ordinal), "a byte offset lands on the next line");
    }

    /// <summary>Reading at the end returns nothing and keeps the offset; a shorter file restarts at zero.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Reading_at_the_end_is_empty_and_a_shrunk_file_restarts()
    {
        var reader = Create();
        var path = Write("d", "x\ny\n");
        var end = (await reader.ReadFrom(path, 0, _none)).Value.NextOffset;

        var atEnd = await reader.ReadFrom(path, end, _none);
        var shrunk = await reader.ReadFrom(Write("d", "z\n"), end, _none);

        Expect.That(atEnd.Value.Lines.Count == 0 && atEnd.Value.NextOffset == end, "nothing new");
        Expect.That(shrunk.Value.Lines.SequenceEqual(["z"], StringComparer.Ordinal), "an offset past the end restarts from the beginning");
    }

    /// <summary>A missing transcript is AW-META-004.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_transcript_is_an_error()
    {
        var reader = Create();

        var chunk = await reader.ReadFrom("/no/such/transcript.jsonl", 0, _none);

        Expect.That(chunk.IsError && chunk.Error.Code == MetaErrors.TranscriptUnreadable.Code, "AW-META-004");
    }
}
