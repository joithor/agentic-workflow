using System.Text.Json;
using Aw.Meta.Core.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Host.Commands;

/// <summary>
/// Command name → entry. To add a command, write its static Entry and list it in <see cref="Entries"/>; parsing, the team-config
/// check and error output are done here.
/// </summary>
internal static class CommandTable
{
    private static readonly CommandEntry[] _entries =
    [
        RoleSetCommand.Entry,
        RoleShowCommand.Entry,
        TeamInitCommand.Entry,
        TeamShowCommand.Entry,
        SyncInboxCommand.Entry,
        StopCheckCommand.Entry,
        OutboxRecordCommand.Entry,
        InboxCommand.Entry,
        InboxDismissCommand.Entry,
        InboxDeferCommand.Entry,
        LanesRefreshCommand.Entry,
        LanesCommand.Entry,
        OwnerCommand.Entry,
        OwnerAnswerCommand.Entry,
        OwnerAskCommand.Entry,
        LogAppendCommand.Entry,
        StatusCommand.Entry,
        GateRunCommand.Entry,
        GateRerunCommand.Entry,
        MergeCheckCommand.Entry,
        MergeRunCommand.Entry,
        HealthCommand.Entry,
        WatchCommand.Entry,
        WorktreeListCommand.Entry,
        WorktreeSweepCommand.Entry,
        LaneSendCommand.Entry,
        LaneInboxCommand.Entry,
        LaneStopCheckCommand.Entry,
        LaneReplyCommand.Entry,
    ];

    internal static IReadOnlyList<CommandEntry> Entries => _entries;

    internal static async Task<int> Run(string[] args, IServiceProvider provider, JsonSerializerOptions json, ConsoleIo io)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0)
        {
            return CommandOutput.Usage(io, "a command is required", "<command> [arguments] (" + string.Join(", ", _entries.Select(e => e.Name)) + ")");
        }

        if (_entries.FirstOrDefault(e => e.Name == args[0]) is not { } entry)
        {
            return CommandOutput.Usage(io, $"unknown command '{args[0]}'", "<command> [arguments]");
        }

        if (CommandLine.Parse(args[1..], entry.Spec, out var problem) is not { } line)
        {
            return CommandOutput.Usage(io, problem, entry.Spec.Usage);
        }

        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var context = new CommandContext(line, scope.ServiceProvider, json, io);
            if (entry.NeedsConfig && (await scope.ServiceProvider.GetRequiredService<IMetaConfig>().Load(CancellationToken.None).ConfigureAwait(false)) is { IsError: true } missing)
            {
                return CommandOutput.Fail(context, missing.Error);
            }

            return await entry.Run(context).ConfigureAwait(false);
        }
    }
}
