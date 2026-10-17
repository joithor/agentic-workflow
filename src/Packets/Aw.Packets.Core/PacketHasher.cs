using System.Text.Json;
using Aw.Kernel;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Kinds.Scoping;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>The one canonicalization, for any kind: reads the packet and computes the hash 'freeze' records, with no checks run.</summary>
/// <param name="builds">Reads a build packet.</param>
/// <param name="scoping">Reads a scope packet.</param>
/// <param name="lines">Reads a line packet.</param>
/// <param name="json">The package's JSON options.</param>
public sealed class PacketHasher(IPacketSource builds, IScopingPacketSource scoping, ILinePacketSource lines, JsonSerializerOptions json)
{
    /// <summary>Hashes the packet in <paramref name="packetDirectory"/>.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="packetDirectory">The packet directory.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The hash; the read error (AW-PACKETS-002, -003, -004, -008) when the packet can't be read.</returns>
    public async ValueTask<Result<HashedPacket>> Hash(PacketKind kind, string packetDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (kind == PacketKinds.Scoping)
        {
            var files = await scoping.Read(packetDirectory, cancellationToken).ConfigureAwait(false);
            return files.IsError
                ? files.Error
                : PacketDigest.Compute(files.Value).Map(hash => new HashedPacket(hash, [.. kind.Files.Select(static f => f.Name).Order(StringComparer.Ordinal)], Identity: null));
        }

        if (kind.IsLine)
        {
            var read = await lines.Read(kind, packetDirectory, cancellationToken).ConfigureAwait(false);
            return read.IsError
                ? read.Error
                : PacketDigest.Compute(kind, read.Value).Map(hash => new HashedPacket(
                    hash,
                    [.. read.Value.Present.Keys.Where(k => kind.Files.Any(f => f.Name == k) || kind.Directories.Any(d => k.StartsWith(d.Prefix, StringComparison.Ordinal))).Order(StringComparer.Ordinal)],
                    LinePacketIdentity.Of(kind, read.Value, json)));
        }

        var loaded = await builds.Load(packetDirectory, cancellationToken).ConfigureAwait(false);
        return loaded.IsError
            ? loaded.Error
            : PacketDigest.Compute(loaded.Value).Map(hash => new HashedPacket(
                hash,
                [.. loaded.Value.Files.Keys.Order(StringComparer.Ordinal)],
                new PacketIdentity(loaded.Value.Packet.Manifest.Repo, loaded.Value.Packet.Manifest.Id)));
    }
}
