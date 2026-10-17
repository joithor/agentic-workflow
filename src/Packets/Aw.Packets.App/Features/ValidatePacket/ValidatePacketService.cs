using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Features.ValidatePacket;

internal sealed class ValidatePacketService(PacketInspector inspector, ISizesSource sizeSource) : IService<ValidatePacketRequest, ValidatePacketResponse>
{
    public async ValueTask<Result<ValidatePacketResponse>> Handle(ValidatePacketRequest request, CancellationToken cancellationToken)
    {
        SizesFile? sizes = null;
        if (request.SizesFile is { } path)
        {
            var read = await sizeSource.Read(path, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            sizes = read.Value;
        }

        var inspection = await inspector.Inspect(request.PacketDirectory, request.RepoDirectory ?? request.PacketDirectory, sizes, cancellationToken)
            .ConfigureAwait(false);
        return inspection.Map(static i => new ValidatePacketResponse(1, i.Loaded.Packet.Manifest.Id, i.Passed, i.Checks));
    }
}
