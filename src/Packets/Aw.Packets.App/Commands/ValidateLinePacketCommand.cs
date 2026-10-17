using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.ValidateLinePacket;
using Aw.Packets.Core.Kinds;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>'validate --kind design|plan|phase|proof|review|handoff': the line kind's schema checks and PKT-BASE-001.</summary>
internal static class ValidateLinePacketCommand
{
    internal static async Task<int> Run(PacketKind kind, IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (PacketArguments.Parse(args) is not { } arguments)
        {
            return CommandOutput.Usage($"{ValidatePacketCommand.Name} --kind {kind.Name} {PacketArguments.Shape}");
        }

        var service = services.GetRequiredService<IService<ValidateLinePacketRequest, ValidateLinePacketResponse>>();
        var result = await service.Handle(new ValidateLinePacketRequest(kind.Name, arguments.PacketDirectory, arguments.RepoDirectory), CancellationToken.None)
            .ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Error(result.Error, arguments.Json, json);
        }

        var report = result.Value;
        if (arguments.Json)
        {
            return CommandOutput.Json(report, report.Passed, json);
        }

        Console.Out.WriteLine($"aw-packets validate: {kind.Name} packet {report.Packet?.Value ?? "(no packet.json)"} — {CommandOutput.Summary(report.Checks)}");
        CommandOutput.Checks(report.Checks);
        return report.Passed ? 0 : 1;
    }
}
