using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Scoping;

namespace Aw.Packets.App.Features.HashPacket;

/// <summary>The one canonicalization: the hash 'freeze' records, for any kind, with no checks run.</summary>
internal sealed class HashPacketService(IPacketSource builds, IScopingPacketSource scoping) : IService<HashPacketRequest, HashPacketResponse>
{
    public async ValueTask<Result<HashPacketResponse>> Handle(HashPacketRequest request, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Kind, PacketKinds.Scoping.Name, StringComparison.Ordinal))
        {
            var files = await scoping.Read(request.PacketDirectory, cancellationToken).ConfigureAwait(false);
            return files.IsError
                ? files.Error
                : PacketDigest.Compute(files.Value).Map(hash => Response(request.Kind, hash, PacketKinds.Scoping.Files.Select(static f => f.Name)));
        }

        var loaded = await builds.Load(request.PacketDirectory, cancellationToken).ConfigureAwait(false);
        return loaded.IsError
            ? loaded.Error
            : PacketDigest.Compute(loaded.Value).Map(hash => Response(request.Kind, hash, loaded.Value.Files.Keys));
    }

    private static HashPacketResponse Response(string kind, Core.Values.PacketHash hash, IEnumerable<string> files) =>
        new(1, kind, hash, PacketDigest.Version, [.. files.Order(StringComparer.Ordinal)]);
}
