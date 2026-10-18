using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IDigestFiles contract, run against the in-memory fake and the file adapter.</summary>
public abstract class DigestFilesContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh store plus a way to write a digest.</summary>
    /// <returns>The store, the writer (name, content, modified time) returning the path.</returns>
    protected abstract (IDigestFiles Files, Func<string, string, DateTimeOffset, string> Put) Create();

    /// <summary>A missing digest reads as null.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_digest_reads_as_null()
    {
        var (files, _) = Create();

        var stat = await files.Stat("/no/such/digest.md", _none);

        Expect.That(stat.IsSuccess && stat.Value is null, "null");
    }

    /// <summary>A written digest reports its size.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_written_digest_reports_its_size_and_an_empty_one_is_length_zero()
    {
        var (files, put) = Create();
        var mtime = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        var full = put("a.md", "hello", mtime);
        var empty = put("b.md", string.Empty, mtime);

        var a = await files.Stat(full, _none);
        var b = await files.Stat(empty, _none);

        Expect.That(a.Value!.Length == 5 && b.Value!.Length == 0, "sizes");
        Expect.That(a.Value.ModifiedAt == mtime && b.Value!.ModifiedAt == mtime, "the modified time");
    }
}
