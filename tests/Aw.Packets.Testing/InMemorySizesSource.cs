using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Testing;

/// <summary>The in-memory <see cref="ISizesSource"/>: sizes files as bytes by path, parsed by the same reader as the disk adapter.</summary>
internal sealed class InMemorySizesSource : ISizesSource
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>Adds (or replaces) a file.</summary>
    /// <param name="path">The path.</param>
    /// <param name="bytes">Its bytes.</param>
    /// <returns>This fake.</returns>
    public InMemorySizesSource With(string path, byte[] bytes)
    {
        _files[path] = bytes;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<SizesFile>> Read(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_files.TryGetValue(path, out var bytes)
            ? SizesReader.Parse(path, bytes, PacketFiles.Json)
            : PacketsErrors.SizesUnusable.With($"No sizes file at {path}."));
}
