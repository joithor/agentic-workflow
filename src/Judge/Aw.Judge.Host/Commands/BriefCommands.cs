namespace Aw.Judge.Host.Commands;

/// <summary>'judge brief &lt;sub&gt; …': routes the brief subcommands (TS cli.ts case "brief").</summary>
internal static class BriefCommands
{
    internal const string Name = "brief";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services) =>
        (args.Count > 0 ? args[0] : null) switch
        {
            SaveBriefCommand.Name => await SaveBriefCommand.Run(services).ConfigureAwait(false),
            _ => CommandOutput.Emit(1, "", "usage: judge brief save|get|map-by-name|map-by-dispatch|set-agent-id ..."),
        };
}
