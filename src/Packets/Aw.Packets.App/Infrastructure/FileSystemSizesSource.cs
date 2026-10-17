using System.Text.Json;
using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Infrastructure;

/// <summary>Reads a sizes file from disk (a regular file; nothing else is read) and parses it with <see cref="SizesReader"/>.</summary>
/// <param name="json">The package's JSON options.</param>
internal sealed class FileSystemSizesSource(JsonSerializerOptions json) : ISizesSource
{
    /// <summary>The largest sizes file read: it is five small objects, so anything bigger is not one.</summary>
    internal const int MaxBytes = 64 * 1024;

    public async ValueTask<Result<SizesFile>> Read(string path, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return PacketsErrors.SizesUnusable.With($"No sizes file at {path}.");
            }

            if (info.Length > MaxBytes)
            {
                return PacketsErrors.SizesUnusable.With($"{path} is larger than {MaxBytes} bytes; a sizes file holds five small objects.");
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return SizesReader.Parse(path, bytes, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PacketsErrors.SizesUnusable.With($"The sizes file {path} can't be read: {ex.Message}");
        }
    }
}
