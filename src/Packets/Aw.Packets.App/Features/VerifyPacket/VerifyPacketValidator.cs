using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.VerifyPacket;

internal sealed class VerifyPacketValidator : IValidator<VerifyPacketRequest>
{
    public Result<Unit> Validate(VerifyPacketRequest value)
    {
        if (string.IsNullOrWhiteSpace(value.PacketDirectory) || PacketKinds.Find(value.Kind) is null)
        {
            return PacketsErrors.Invalid.With("The packet directory must not be empty, and the kind must be one of: " + string.Join(", ", PacketKinds.All.Select(static k => k.Name)) + ".");
        }

        var scoping = string.Equals(value.Kind, PacketKinds.Scoping.Name, StringComparison.Ordinal);
        if (scoping && !value.Candidate && (value.RepoSlug is null || !RepoSlug.TryFrom(value.RepoSlug, out _) || value.Instance is null || !PacketId.TryFrom(value.Instance, out _)))
        {
            return PacketsErrors.Invalid.With("A scope packet has no packet.json: to find its freeze record, give the repository ('--repo-slug <owner/name>') and the scoping instance ('--instance <slug>') it was frozen under, or use '--candidate'.");
        }

        return Unit.Value;
    }
}
