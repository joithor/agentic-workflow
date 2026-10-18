using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;

namespace Aw.Packets.App.Features.ValidatePacket;

internal sealed class ValidatePacketValidator : IValidator<ValidatePacketRequest>
{
    public Result<Unit> Validate(ValidatePacketRequest value) =>
        string.IsNullOrWhiteSpace(value.PacketDirectory) || value.RepoDirectory is { Length: 0 } || value.SizesFile is { Length: 0 }
            ? PacketsErrors.Invalid.With("The packet directory, and the repository directory and the sizes file when given, must not be empty.")
            : Unit.Value;
}
