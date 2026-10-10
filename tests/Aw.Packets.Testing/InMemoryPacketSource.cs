using Aw.Kernel;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Ports;

namespace Aw.Packets.Testing;

/// <summary>The in-memory <see cref="IPacketSource"/>: packet directories as file maps, parsed by the same loader as the disk adapter.</summary>
internal sealed class InMemoryPacketSource : IPacketSource
{
    private readonly Dictionary<string, Dictionary<string, byte[]>> _directories = new(StringComparer.Ordinal);

    /// <summary>Adds (or replaces) a packet directory.</summary>
    /// <param name="directory">The directory name.</param>
    /// <param name="files">Its files by relative path.</param>
    /// <returns>This fake.</returns>
    public InMemoryPacketSource With(string directory, Dictionary<string, byte[]> files)
    {
        _directories[directory] = files;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<LoadedPacket>> Load(string packetDirectory, CancellationToken cancellationToken)
    {
        var files = _directories.GetValueOrDefault(packetDirectory) ?? [];
        return ValueTask.FromResult(PacketLoading.Load(
            path => files.TryGetValue(path, out var bytes) ? new ReadOnlyMemory<byte>(bytes) : (ReadOnlyMemory<byte>?)null,
            PacketFiles.Json));
    }
}
