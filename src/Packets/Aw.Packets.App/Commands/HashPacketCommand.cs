using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.HashPacket;
using Aw.Packets.Core.Kinds;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>The 'hash' command: the packet's canonical hash, with no checks run. The text is the hash alone.</summary>
internal static class HashPacketCommand
{
    internal const string Name = "hash";
    private const string Shape = "<packet-dir> [--kind build|scoping] [--json]";

    internal static async Task<int> Run(PacketKind kind, IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (SimpleArguments.Parse(args) is not { } arguments)
        {
            return CommandOutput.Usage($"{Name} {Shape}");
        }

        var service = services.GetRequiredService<IService<HashPacketRequest, HashPacketResponse>>();
        var result = await service.Handle(new HashPacketRequest(kind.Name, arguments.PacketDirectory), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Error(result.Error, arguments.Json, json);
        }

        if (arguments.Json)
        {
            return CommandOutput.Json(result.Value, passed: true, json);
        }

        Console.Out.WriteLine(result.Value.Hash.Value);
        return 0;
    }
}
