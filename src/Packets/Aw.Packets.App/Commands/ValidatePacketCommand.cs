using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.ValidatePacket;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>The 'validate' command: binds its arguments, calls ValidatePacket and maps the result to an exit code.</summary>
internal static class ValidatePacketCommand
{
    internal const string Name = "validate";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (PacketArguments.Parse(args, takesSizes: true) is not { } arguments)
        {
            return CommandOutput.Usage($"{Name} {PacketArguments.ValidateShape}");
        }

        var service = services.GetRequiredService<IService<ValidatePacketRequest, ValidatePacketResponse>>();
        var result = await service.Handle(new ValidatePacketRequest(arguments.PacketDirectory, arguments.RepoDirectory, arguments.SizesFile), CancellationToken.None)
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

        Console.Out.WriteLine($"aw-packets validate: packet {report.Packet.Value} — {CommandOutput.Summary(report.Checks)}");
        CommandOutput.Checks(report.Checks);
        return report.Passed ? 0 : 1;
    }
}
