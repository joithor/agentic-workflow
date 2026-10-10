using System.Text.Json;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge config get|set …': routes the config subcommands (TS cli.ts case "config").</summary>
internal static class ConfigCommands
{
    internal const string Name = "config";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json) =>
        (args.Count > 0 ? args[0] : null) switch
        {
            GetConfigCommand.Name => await GetConfigCommand.Run(services, json).ConfigureAwait(false),
            _ => CommandOutput.Emit(1, "", "usage: judge config get|set <question> <enabled|threshold> <value>"),
        };
}
