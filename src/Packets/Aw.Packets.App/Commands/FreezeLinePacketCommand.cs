using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.FreezeLinePacket;
using Aw.Packets.Core.Kinds;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>'freeze --kind design|plan|phase|proof|review|handoff': validates a line packet and records its hash under packets/&lt;owner&gt;/&lt;name&gt;/&lt;id&gt;/frozen/.</summary>
internal static class FreezeLinePacketCommand
{
    internal static async Task<int> Run(PacketKind kind, IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (PacketArguments.Parse(args, takesBy: true) is not { } arguments)
        {
            return CommandOutput.Usage($"{FreezePacketCommand.Name} --kind {kind.Name} {PacketArguments.FreezeShape}");
        }

        var service = services.GetRequiredService<IService<FreezeLinePacketRequest, FreezeLinePacketResponse>>();
        var result = await service.Handle(new FreezeLinePacketRequest(kind.Name, arguments.PacketDirectory, arguments.RepoDirectory, arguments.By), CancellationToken.None)
            .ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Error(result.Error, arguments.Json, json);
        }

        var frozen = result.Value;
        if (arguments.Json)
        {
            return CommandOutput.Json(frozen, frozen.Frozen, json);
        }

        Print(kind, frozen);
        return frozen.Frozen ? 0 : 1;
    }

    private static void Print(PacketKind kind, FreezeLinePacketResponse frozen)
    {
        var name = frozen.Packet?.Value ?? "(no packet.json)";
        var outcome = frozen.Frozen
            ? $"{(frozen.AlreadyFrozen ? "already frozen" : "frozen")} {frozen.Hash?.Value} (packets/{frozen.Location})"
            : $"not frozen: {CommandOutput.Summary(frozen.Checks)}";
        Console.Out.WriteLine($"aw-packets freeze: {kind.Name} packet {name} — {outcome}");
        if (!frozen.Frozen)
        {
            CommandOutput.Checks(frozen.Checks);
        }
    }
}
