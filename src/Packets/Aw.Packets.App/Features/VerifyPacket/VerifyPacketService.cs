using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;

namespace Aw.Packets.App.Features.VerifyPacket;

internal sealed class VerifyPacketService(PacketHasher hasher, IFreezeLog log) : IService<VerifyPacketRequest, VerifyPacketResponse>
{
    public ValueTask<Result<VerifyPacketResponse>> Handle(VerifyPacketRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"VerifyPacket is not implemented yet ({hasher.GetType().Name}, {log.GetType().Name}).");
}
