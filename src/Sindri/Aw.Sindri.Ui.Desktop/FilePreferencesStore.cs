using System.Globalization;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The desktop adapter of <see cref="IPreferencesStore"/>: preferences.json in an owner-only directory. A write goes to a
/// new 0600 temp file beside it, flushed to disk, then renamed over the file, so a reader (or a crash) sees the old
/// contents or the new, and the file's mode is always the temp file's.
/// </summary>
/// <param name="path">The file.</param>
internal sealed class FilePreferencesStore(string path) : IPreferencesStore
{
    private static int _writes;

    public string Location => path;

    public async ValueTask<byte[]?> Read(CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async ValueTask Write(byte[] contents, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!; // why: the store is built over a file path, never a root
        OwnerOnly.CreateDirectory(directory);
        var temp = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $".{Path.GetFileName(path)}.{Environment.ProcessId}.{Interlocked.Increment(ref _writes)}.tmp"));
        try
        {
            var stream = OwnerOnly.Open(temp, FileMode.CreateNew, FileShare.None);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(contents, cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
