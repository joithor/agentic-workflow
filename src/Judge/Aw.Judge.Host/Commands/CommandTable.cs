using System.Collections.Frozen;
using System.Text.Json;
using Aw.Judge.Core.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// Command name → handler. The table first takes the providers allowlist out of argv ('--providers a,b' anywhere, else
/// AW_JUDGE_PROVIDERS), then checks each command's argument count (a bad list or a wrong count is a usage error, 64);
/// each handler binds its arguments, calls one service and maps the result to an exit code (docs/csharp/judge.md).
/// </summary>
internal static class CommandTable
{
    internal const string Synopsis = "<command> [args] | judge <question> < input.json";

    // The first words the switch routes; any other is a question.
    private static readonly FrozenSet<string> _routed = FrozenSet.Create(StringComparer.Ordinal, GetHealthCommand.Name, BriefCommands.Name, ExplainDecisionCommand.Name, UndoDecisionCommand.Name, ApproveDecisionCommand.Name, ConfigCommands.Name);

    internal static async Task<int> Run(string[] argv, IServiceProvider provider, JsonSerializerOptions json)
    {
        var allowlist = ProvidersAllowlist.Parse(argv, provider.GetRequiredService<ProvidersVariable>().Value);
        if (allowlist.IsError)
        {
            return CommandOutput.Usage(Synopsis, allowlist.Error.Message);
        }

        string[] args = [.. allowlist.Value.Arguments];
        if (args.Length == 0)
        {
            return CommandOutput.Usage(Synopsis);
        }

        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var rest = args[1..];
            var services = scope.ServiceProvider;
            return args[0] switch
            {
                _ when !_routed.Contains(args[0]) => await QuestionCommand.Run(args[0], rest, allowlist.Value.Allowed, services, json).ConfigureAwait(false),
                GetHealthCommand.Name when rest.Length == 0 => await GetHealthCommand.Run(services, json).ConfigureAwait(false),
                GetHealthCommand.Name => CommandOutput.Usage("health"),
                BriefCommands.Name => await BriefCommands.Run(rest, services, json).ConfigureAwait(false),
                ConfigCommands.Name => await ConfigCommands.Run(rest, services, json).ConfigureAwait(false),
                _ when rest.Length != 1 => CommandOutput.Usage($"{args[0]} <decision-id>"),
                ExplainDecisionCommand.Name => await ExplainDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
                UndoDecisionCommand.Name => await UndoDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
                _ => await ApproveDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
            };
        }
    }
}
