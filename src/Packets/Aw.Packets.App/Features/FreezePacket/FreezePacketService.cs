using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Packets.App.Features.FreezePacket;

internal sealed class FreezePacketService : IService<FreezePacketRequest, FreezePacketResponse>
{
    public ValueTask<Result<FreezePacketResponse>> Handle(FreezePacketRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("FreezePacket: make FreezePacketTests pass");
}
