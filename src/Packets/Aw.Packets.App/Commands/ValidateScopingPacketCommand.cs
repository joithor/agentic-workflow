using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.ValidateScopingPacket;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>'validate --kind scoping': the schema checks (PKT-SCP-001..005) over a scope packet directory.</summary>
internal static class ValidateScopingPacketCommand
{
    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (SimpleArguments.Parse(args) is not { } arguments)
        {
            return CommandOutput.Usage($"{ValidatePacketCommand.Name} --kind scoping <packet-dir> [--json]");
        }

        var service = services.GetRequiredService<IService<ValidateScopingPacketRequest, ValidateScopingPacketResponse>>();
        var result = await service.Handle(new ValidateScopingPacketRequest(arguments.PacketDirectory), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Error(result.Error, arguments.Json, json);
        }

        var report = result.Value;
        if (arguments.Json)
        {
            return CommandOutput.Json(report, report.Passed, json);
        }

        Console.Out.WriteLine($"aw-packets validate: scoping packet — {CommandOutput.Summary(report.Checks)}");
        CommandOutput.Checks(report.Checks);
        return report.Passed ? 0 : 1;
    }
}
