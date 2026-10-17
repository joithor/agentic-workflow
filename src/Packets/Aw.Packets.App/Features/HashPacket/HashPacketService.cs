using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Kinds;

namespace Aw.Packets.App.Features.HashPacket;

/// <summary>The one canonicalization: the hash 'freeze' records, for any kind, with no checks run.</summary>
internal sealed class HashPacketService(PacketHasher hasher) : IService<HashPacketRequest, HashPacketResponse>
{
    public async ValueTask<Result<HashPacketResponse>> Handle(HashPacketRequest request, CancellationToken cancellationToken)
    {
        var kind = PacketKinds.Find(request.Kind)!;   // why: the validator accepted the kind
        var hashed = await hasher.Hash(kind, request.PacketDirectory, cancellationToken).ConfigureAwait(false);
        return hashed.Map(h => new HashPacketResponse(1, kind.Name, h.Hash, PacketDigest.Version, h.Files));
    }
}
