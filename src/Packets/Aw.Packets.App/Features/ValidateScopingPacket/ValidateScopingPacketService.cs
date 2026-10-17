using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Scoping;

namespace Aw.Packets.App.Features.ValidateScopingPacket;

internal sealed class ValidateScopingPacketService(IScopingPacketSource source, JsonSerializerOptions json)
    : IService<ValidateScopingPacketRequest, ValidateScopingPacketResponse>
{
    public async ValueTask<Result<ValidateScopingPacketResponse>> Handle(ValidateScopingPacketRequest request, CancellationToken cancellationToken)
    {
        var files = await source.Read(request.PacketDirectory, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error;
        }

        var checks = ScopingChecks.Run(files.Value, json);
        return new ValidateScopingPacketResponse(1, PacketKinds.Scoping.Name, checks.All(static c => c.State is not (CheckState.Fail or CheckState.Omitted)), checks);
    }
}
