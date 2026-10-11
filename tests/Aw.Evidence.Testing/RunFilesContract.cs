using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IRunFiles contract: the in-memory fake and every real adapter pass the same tests.</summary>
public abstract class RunFilesContract
{
    /// <summary>An absolute directory the store may write under, unique to the test.</summary>
    protected abstract string Root { get; }

    /// <summary>Creates the store under test.</summary>
    /// <returns>The store.</returns>
    protected abstract IRunFiles Create();

    /// <summary>A written file reads back byte for byte, its directory created on the way.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_written_file_reads_back_unchanged()
    {
        var files = Create();
        var path = RunPath.Combine(Root, "run-1/nested/summary.json");

        Expect.That((await files.WriteAsync(path, "abc"u8.ToArray(), CancellationToken.None)).IsSuccess, "a write succeeds");

        Expect.That((await files.ReadAsync(path, CancellationToken.None)).Value.SequenceEqual("abc"u8.ToArray()), "a written file reads back unchanged");
        Expect.That((await files.SizeAsync(path, CancellationToken.None)).Value == 3, "the size is the byte count");
    }

    /// <summary>Writing again replaces the whole content.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Writing_again_replaces_the_content()
    {
        var files = Create();
        var path = RunPath.Combine(Root, "a.png");
        _ = await files.WriteAsync(path, "long"u8.ToArray(), CancellationToken.None);

        _ = await files.WriteAsync(path, "z"u8.ToArray(), CancellationToken.None);

        Expect.That((await files.ReadAsync(path, CancellationToken.None)).Value.SequenceEqual("z"u8.ToArray()), "a second write replaces the content");
    }

    /// <summary>A missing file is AW-EVIDENCE-010 for read and size.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_file_is_not_found()
    {
        var files = Create();
        var path = RunPath.Combine(Root, "missing.json");

        Expect.That((await files.ReadAsync(path, CancellationToken.None)).Error.Code == EvidenceErrors.FileNotFound.Code, "reading a missing file is AW-EVIDENCE-010");
        Expect.That((await files.SizeAsync(path, CancellationToken.None)).Error.Code == EvidenceErrors.FileNotFound.Code, "sizing a missing file is AW-EVIDENCE-010");
    }

    /// <summary>Deleting removes the file; deleting a missing file succeeds.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Delete_removes_a_file_and_tolerates_a_missing_one()
    {
        var files = Create();
        var path = RunPath.Combine(Root, "parity.json");
        _ = await files.WriteAsync(path, "x"u8.ToArray(), CancellationToken.None);

        Expect.That((await files.DeleteAsync(path, CancellationToken.None)).IsSuccess, "a delete succeeds");
        Expect.That((await files.DeleteAsync(path, CancellationToken.None)).IsSuccess, "deleting a missing file succeeds");
        Expect.That((await files.ReadAsync(path, CancellationToken.None)).Error.Code == EvidenceErrors.FileNotFound.Code, "a deleted file is gone");
    }

    /// <summary>Creating a directory twice succeeds, and files can be written into it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Creating_a_directory_is_idempotent()
    {
        var files = Create();
        var dir = RunPath.Combine(Root, "videos/deep");

        Expect.That((await files.CreateDirectoryAsync(dir, CancellationToken.None)).IsSuccess, "a directory is created");
        Expect.That((await files.CreateDirectoryAsync(dir, CancellationToken.None)).IsSuccess, "creating it again succeeds");
        Expect.That((await files.WriteAsync(RunPath.Combine(dir, "v.webm"), "v"u8.ToArray(), CancellationToken.None)).IsSuccess, "files can be written into it");
    }
}
