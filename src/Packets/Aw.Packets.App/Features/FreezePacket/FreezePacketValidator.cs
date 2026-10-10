using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;

namespace Aw.Packets.App.Features.FreezePacket;

internal sealed class FreezePacketValidator : IValidator<FreezePacketRequest>
{
    public Result<Unit> Validate(FreezePacketRequest value) =>
        string.IsNullOrWhiteSpace(value.PacketDirectory) || value.RepoDirectory is { Length: 0 }
            ? PacketsErrors.Invalid.With("The packet directory, and the repository directory when given, must not be empty.")
            : Unit.Value;
}
