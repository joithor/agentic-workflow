using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Files;

/// <summary>Run files on the local disk. A write goes to a sibling temp file first and is moved over the target, so a reader never sees half a file.</summary>
internal sealed class FileRunFiles : IRunFiles
{
    /// <inheritdoc/>
    public async ValueTask<Result<byte[]>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return EvidenceErrors.FileNotFound.With($"{path} does not exist");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Unavailable("read", path, ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var temporary = string.Create(CultureInfo.InvariantCulture, $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        var moved = false;
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            await File.WriteAllBytesAsync(temporary, content, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
            moved = true;
            return Results.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Unavailable("write", path, ex);
        }
        finally
        {
            if (!moved)
            {
                DeleteQuietly(temporary);   // a failed or cancelled write leaves no .tmp behind
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DeleteAsync(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Attempt("delete", path, () => File.Delete(path)));

    /// <inheritdoc/>
    public ValueTask<Result<long>> SizeAsync(string path, CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        return ValueTask.FromResult<Result<long>>(file.Exists ? file.Length : EvidenceErrors.FileNotFound.With($"{path} does not exist"));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> CreateDirectoryAsync(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Attempt("create", path, () => Directory.CreateDirectory(path)));

    private static Result<Unit> Attempt(string verb, string path, Action action)
    {
        try
        {
            action();
            return Results.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Unavailable(verb, path, ex);
        }
    }

    private static Error Unavailable(string verb, string path, Exception ex) =>
        EvidenceErrors.FileUnavailable.With($"can't {verb} {path}: {ex.Message}");

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing to clean up (its directory was never made), or not ours to remove.
        }
    }
}
