using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>Command name → handler. Each handler binds its arguments, calls one service and maps the result to an exit code.</summary>
internal static class CommandTable
{
    internal static async Task<int> Run(string[] args, IServiceProvider provider, JsonSerializerOptions json)
    {
        if (args.Length == 0)
        {
            return CommandOutput.Usage("validate|freeze <packet-dir> [--repo <dir>] [--json]");
        }

        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var rest = args[1..];
            return args[0] switch
            {
                ValidatePacketCommand.Name => await ValidatePacketCommand.Run(rest, scope.ServiceProvider, json).ConfigureAwait(false),
                FreezePacketCommand.Name => await FreezePacketCommand.Run(rest, scope.ServiceProvider, json).ConfigureAwait(false),
                _ => CommandOutput.Usage($"unknown command '{args[0]}'"),
            };
        }
    }
}
