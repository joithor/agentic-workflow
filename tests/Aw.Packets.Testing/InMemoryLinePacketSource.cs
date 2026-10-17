using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;

namespace Aw.Packets.Testing;

/// <summary>The in-memory <see cref="ILinePacketSource"/>: directories as file maps, kept honest by <see cref="LinePacketSourceContract"/>.</summary>
internal sealed class InMemoryLinePacketSource : ILinePacketSource
{
    private readonly Dictionary<string, Dictionary<string, byte[]>> _directories = new(StringComparer.Ordinal);

    /// <summary>Adds (or replaces) a packet directory.</summary>
    /// <param name="directory">The directory name.</param>
    /// <param name="files">Its files by relative path; a path with a '/' puts its first segment in the directory as a sub-directory.</param>
    /// <returns>This fake.</returns>
    public InMemoryLinePacketSource With(string directory, Dictionary<string, byte[]> files)
    {
        _directories[directory] = files;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<LinePacketFiles>> Read(PacketKind kind, string packetDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (!_directories.TryGetValue(packetDirectory, out var files))
        {
            return ValueTask.FromResult<Result<LinePacketFiles>>(PacketsErrors.NotFound.With($"No packet directory at {packetDirectory}."));
        }

        var listed = kind.Files.Select(static f => f.Name).ToHashSet(StringComparer.Ordinal);
        var present = files
            .Where(f => listed.Contains(f.Key) || kind.Directories.Any(d => f.Key.StartsWith(d.Prefix, StringComparison.Ordinal)))
            .ToDictionary(f => f.Key, f => new ReadOnlyMemory<byte>(f.Value), StringComparer.Ordinal);
        var directories = kind.Directories.Select(static d => d.Prefix).ToHashSet(StringComparer.Ordinal);
        var unlisted = files.Keys
            .Select(static path => path.Contains('/', StringComparison.Ordinal) ? path[..path.IndexOf('/', StringComparison.Ordinal)] + "/" : path)
            .Where(entry => !listed.Contains(entry) && !directories.Contains(entry))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        return ValueTask.FromResult<Result<LinePacketFiles>>(new LinePacketFiles(present, unlisted));
    }
}
