using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>Command name → handler. Each handler binds its arguments, calls one service and maps the result to an exit code.</summary>
internal static class CommandTable
{
    // The first words the switch routes; any other is a question (TS cli.ts's default branch).
    private static readonly FrozenSet<string> _routed = FrozenSet.Create(StringComparer.Ordinal, GetHealthCommand.Name);

    internal static async Task<int> Run(string[] args, IServiceProvider provider, JsonSerializerOptions json)
    {
        if (args.Length == 0)
        {
            return CommandOutput.Emit(1, "", "usage: judge <question> < input.json");
        }

        if (!_routed.Contains(args[0]))
        {
            return await QuestionCommand.Run(args[0]).ConfigureAwait(false);
        }

        _ = json;   // the Kernel JSON profile: /new-slice's generated arms pass it to their commands
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var rest = args[1..];
            return args[0] switch
            {
                GetHealthCommand.Name => await GetHealthCommand.Run(scope.ServiceProvider).ConfigureAwait(false),
                _ => CommandOutput.Usage($"unknown command '{args[0]}'"),
            };
        }
    }
}
