using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Packets.App.Features.ValidatePacket;

internal sealed class ValidatePacketValidator : IValidator<ValidatePacketRequest>
{
    public Result<Unit> Validate(ValidatePacketRequest value) => Unit.Value;
}
