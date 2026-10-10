using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Host.Commands;

/// <summary>Command name → handler. Each handler binds its arguments, calls one service and maps the result to an exit code.</summary>
internal static class CommandTable
{
    private static readonly Dictionary<string, (CommandSpec Spec, Func<CommandLine, IServiceProvider, JsonSerializerOptions, Task<int>> Run)> _commands =
        new(StringComparer.Ordinal)
        {
            [InitCommand.Name] = (InitCommand.Spec, InitCommand.Run),
            [AdvanceInvestigateCommand.Name] = (AdvanceInvestigateCommand.Spec, AdvanceInvestigateCommand.Run),
            [AdvanceReproduceCommand.Name] = (AdvanceReproduceCommand.Spec, AdvanceReproduceCommand.Run),
            [AdvanceReportCommand.Name] = (AdvanceReportCommand.Spec, AdvanceReportCommand.Run),
            [StartAttemptCommand.Name] = (StartAttemptCommand.Spec, StartAttemptCommand.Run),
            [RecordCandidateCommand.Name] = (RecordCandidateCommand.Spec, RecordCandidateCommand.Run),
            [RecordRunCommand.Name] = (RecordRunCommand.Spec, RecordRunCommand.Run),
            [JudgeCandidateCommand.Name] = (JudgeCandidateCommand.Spec, JudgeCandidateCommand.Run),
            [RunTestCommand.Name] = (RunTestCommand.Spec, RunTestCommand.Run),
            [RunUiCommand.Name] = (RunUiCommand.Spec, RunUiCommand.Run),
            [StatusCommand.Name] = (StatusCommand.Spec, StatusCommand.Run),
            [ResumeCommand.Name] = (ResumeCommand.Spec, ResumeCommand.Run),
        };

    internal static string Usage { get; } =
        "bugfix-state <command> --state <dir> [options]\n" + string.Join('\n', _commands.Values.Select(static c => "  " + c.Spec.Usage));

    internal static async Task<int> Run(string[] args, IServiceProvider provider, JsonSerializerOptions json)
    {
        var take = args.Length > 1 && args[0] == "advance" ? 2 : 1;
        var name = string.Join(' ', args.Take(take));
        if (args.Length == 0 || !_commands.TryGetValue(name, out var command))
        {
            return CommandOutput.Usage(args.Length == 0 ? "no command" : $"unknown command '{name}'", Usage);
        }

        if (CommandLine.Parse(args[take..], command.Spec, out var problem) is not { } line)
        {
            return CommandOutput.Usage(problem, command.Spec.Usage);
        }

        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await command.Run(line, scope.ServiceProvider, json).ConfigureAwait(false);
        }
    }
}
