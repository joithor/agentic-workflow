using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core.Checks;

namespace Aw.Packets.App.Features.ValidatePacket;

internal sealed class ValidatePacketService(PacketInspector inspector) : IService<ValidatePacketRequest, ValidatePacketResponse>
{
    public async ValueTask<Result<ValidatePacketResponse>> Handle(ValidatePacketRequest request, CancellationToken cancellationToken)
    {
        var inspection = await inspector.Inspect(request.PacketDirectory, request.RepoDirectory ?? request.PacketDirectory, cancellationToken)
            .ConfigureAwait(false);
        return inspection.Map(static i => new ValidatePacketResponse(1, i.Loaded.Packet.Manifest.Id, i.Passed, i.Checks));
    }
}
