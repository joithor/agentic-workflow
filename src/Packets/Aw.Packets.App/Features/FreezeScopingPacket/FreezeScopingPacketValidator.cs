using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.FreezeScopingPacket;

internal sealed class FreezeScopingPacketValidator : IValidator<FreezeScopingPacketRequest>
{
    public Result<Unit> Validate(FreezeScopingPacketRequest value)
    {
        if (string.IsNullOrWhiteSpace(value.PacketDirectory))
        {
            return PacketsErrors.Invalid.With("The packet directory must not be empty.");
        }

        if (!RepoSlug.TryFrom(value.RepoSlug, out _))
        {
            return PacketsErrors.Invalid.With("--repo-slug must be 'owner/name' (letters, digits, '.', '_', '-').");
        }

        return PacketId.TryFrom(value.Instance, out _)
            ? Unit.Value
            : PacketsErrors.Invalid.With("--instance must be 1-64 lower-case letters, digits or '-', starting with a letter or digit.");
    }
}
