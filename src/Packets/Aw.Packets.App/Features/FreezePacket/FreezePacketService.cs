using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;

namespace Aw.Packets.App.Features.FreezePacket;

internal sealed class FreezePacketService(PacketInspector inspector, IFreezeLog log, TimeProvider clock)
    : IService<FreezePacketRequest, FreezePacketResponse>
{
    public ValueTask<Result<FreezePacketResponse>> Handle(FreezePacketRequest request, CancellationToken cancellationToken)
    {
        _ = (inspector, log, clock);
        throw new NotSupportedException("FreezePacket: make FreezePacketTests pass");
    }
}
