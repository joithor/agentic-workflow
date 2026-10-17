using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;

namespace Aw.Packets.App.Features.FreezeLinePacket;

internal sealed class FreezeLinePacketValidator : IValidator<FreezeLinePacketRequest>
{
    public Result<Unit> Validate(FreezeLinePacketRequest value) =>
        PacketKinds.Find(value.Kind) is not { IsLine: true } || string.IsNullOrWhiteSpace(value.PacketDirectory) || value.RepoDirectory is { Length: 0 }
            ? PacketsErrors.Invalid.With("The kind must be a line kind, and the packet directory, and the repository directory when given, must not be empty.")
            : Unit.Value;
}
