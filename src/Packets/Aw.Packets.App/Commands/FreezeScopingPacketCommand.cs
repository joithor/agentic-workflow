using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.FreezeScopingPacket;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>'freeze --kind scoping': validates a scope packet and records its hash under the scoping instance.</summary>
internal static class FreezeScopingPacketCommand
{
    private const string Shape = "--kind scoping <packet-dir> --repo-slug <owner/name> --instance <slug> [--json]";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (Parse(args) is not { } arguments)
        {
            return CommandOutput.Usage($"{FreezePacketCommand.Name} {Shape}");
        }

        var service = services.GetRequiredService<IService<FreezeScopingPacketRequest, FreezeScopingPacketResponse>>();
        var result = await service.Handle(new FreezeScopingPacketRequest(arguments.PacketDirectory, arguments.RepoSlug, arguments.Instance), CancellationToken.None)
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
            ? $"aw-packets freeze: scoping packet {frozen.Instance.Value} — {(frozen.AlreadyFrozen ? "already frozen" : "frozen")} {frozen.Hash?.Value} (packets/{frozen.Location})"
            : $"aw-packets freeze: scoping packet {frozen.Instance.Value} — not frozen: {CommandOutput.Summary(frozen.Checks)}");
        if (!frozen.Frozen)
        {
            CommandOutput.Checks(frozen.Checks);
        }

        return frozen.Frozen ? 0 : 1;
    }

    private static (string PacketDirectory, string RepoSlug, string Instance, bool Json)? Parse(IReadOnlyList<string> args)
    {
        if (SimpleArguments.Parse(args, "--repo-slug", "--instance") is not { } parsed
            || !parsed.Values.TryGetValue("--repo-slug", out var slug)
            || !parsed.Values.TryGetValue("--instance", out var instance))
        {
            return null;
        }

        return (parsed.PacketDirectory, slug, instance, parsed.Json);
    }
}
