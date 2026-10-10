using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.FreezePacket;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>The 'freeze' command: binds its arguments, calls FreezePacket and maps the result to an exit code.</summary>
internal static class FreezePacketCommand
{
    internal const string Name = "freeze";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 0)
        {
            return CommandOutput.Usage("freeze");
        }

        var service = services.GetRequiredService<IService<FreezePacketRequest, FreezePacketResponse>>();
        var result = await service.Handle(new FreezePacketRequest(), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
