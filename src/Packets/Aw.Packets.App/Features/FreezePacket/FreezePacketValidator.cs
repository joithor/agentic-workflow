using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Packets.App.Features.FreezePacket;

internal sealed class FreezePacketValidator : IValidator<FreezePacketRequest>
{
    public Result<Unit> Validate(FreezePacketRequest value) => Unit.Value;
}
