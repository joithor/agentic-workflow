using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;

namespace Aw.Packets.App.Features.ValidateLinePacket;

internal sealed class ValidateLinePacketService(LinePacketInspector inspector) : IService<ValidateLinePacketRequest, ValidateLinePacketResponse>
{
    public async ValueTask<Result<ValidateLinePacketResponse>> Handle(ValidateLinePacketRequest request, CancellationToken cancellationToken)
    {
        var kind = PacketKinds.Find(request.Kind)!;   // the validator accepted it
        var inspection = await inspector.Inspect(kind, request.PacketDirectory, request.RepoDirectory ?? request.PacketDirectory, cancellationToken)
            .ConfigureAwait(false);
        return inspection.Map(static i => new ValidateLinePacketResponse(1, i.Kind.Name, i.Manifest?.Id, i.Passed, i.Checks));
    }
}
