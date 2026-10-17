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
        if (PacketArguments.Parse(args, takesBy: true) is not { } arguments)
        {
            return CommandOutput.Usage($"{Name} {PacketArguments.FreezeShape}");
        }

        var service = services.GetRequiredService<IService<FreezePacketRequest, FreezePacketResponse>>();
        var result = await service.Handle(new FreezePacketRequest(arguments.PacketDirectory, arguments.RepoDirectory, arguments.By), CancellationToken.None)
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

        Console.Out.WriteLine(frozen.Frozen
            ? $"aw-packets freeze: packet {frozen.Packet.Value} — {(frozen.AlreadyFrozen ? "already frozen" : "frozen")} {frozen.Hash?.Value} (packets/{frozen.Location})"
            : $"aw-packets freeze: packet {frozen.Packet.Value} — not frozen: {CommandOutput.Summary(frozen.Checks)}");
        CommandOutput.AlreadyFrozenNote(frozen.AlreadyFrozenBy);
        if (!frozen.Frozen)
        {
            CommandOutput.Checks(frozen.Checks);
        }

        return frozen.Frozen ? 0 : 1;
    }
}
