using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;

namespace Aw.Packets.App.Features.HashPacket;

internal sealed class HashPacketValidator : IValidator<HashPacketRequest>
{
    public Result<Unit> Validate(HashPacketRequest value) =>
        string.IsNullOrWhiteSpace(value.PacketDirectory) || PacketKinds.Find(value.Kind) is null
            ? PacketsErrors.Invalid.With("The packet directory must not be empty, and the kind must be one of: " + string.Join(", ", PacketKinds.All.Select(static k => k.Name)) + ".")
            : Unit.Value;
}
