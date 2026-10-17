using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;

namespace Aw.Packets.App.Features.ValidateScopingPacket;

internal sealed class ValidateScopingPacketValidator : IValidator<ValidateScopingPacketRequest>
{
    public Result<Unit> Validate(ValidateScopingPacketRequest value) =>
        string.IsNullOrWhiteSpace(value.PacketDirectory)
            ? PacketsErrors.Invalid.With("The packet directory must not be empty.")
            : Unit.Value;
}
