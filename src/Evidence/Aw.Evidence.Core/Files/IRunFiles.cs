using Aw.Kernel;

namespace Aw.Evidence.Core.Files;

/// <summary>The files a run reads and writes (scripts, manifests, design exports, screenshots, run files). Absolute paths.</summary>
public interface IRunFiles
{
    /// <summary>Reads a whole file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, AW-EVIDENCE-010 when it doesn't exist, or AW-EVIDENCE-011 when it can't be read.</returns>
    public ValueTask<Result<byte[]>> ReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>Writes a whole file, creating its directory, replacing any earlier content in one step.</summary>
    /// <param name="path">The file.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or AW-EVIDENCE-011.</returns>
    public ValueTask<Result<Unit>> WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Deletes a file; a missing file is already deleted.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>Success, or AW-EVIDENCE-011.</returns>
    public ValueTask<Result<Unit>> DeleteAsync(string path, CancellationToken cancellationToken);

    /// <summary>A file's size in bytes.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The size, AW-EVIDENCE-010 or AW-EVIDENCE-011.</returns>
    public ValueTask<Result<long>> SizeAsync(string path, CancellationToken cancellationToken);

    /// <summary>Creates a directory and its parents; an existing one is fine.</summary>
    /// <param name="path">The directory.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>Success, or AW-EVIDENCE-011.</returns>
    public ValueTask<Result<Unit>> CreateDirectoryAsync(string path, CancellationToken cancellationToken);
}
