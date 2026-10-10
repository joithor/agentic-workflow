using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Packets.App.Features.ValidatePacket;

internal sealed class ValidatePacketService : IService<ValidatePacketRequest, ValidatePacketResponse>
{
    public ValueTask<Result<ValidatePacketResponse>> Handle(ValidatePacketRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ValidatePacket: make ValidatePacketTests pass");
}
