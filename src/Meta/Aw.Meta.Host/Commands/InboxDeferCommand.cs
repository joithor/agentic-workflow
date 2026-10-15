using System.Globalization;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.InboxDefer;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'inbox-defer' command.</summary>
internal static class InboxDeferCommand
{
    internal static CommandEntry Entry { get; } = new(
        "inbox-defer",
        new CommandSpec("inbox-defer <id> --reason <text> --until <iso8601|+30m> [--json]", ["reason", "until"], [], [], Positionals: 1),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context) =>
        MessageId.TryFrom(context.Line.Positional(0), out var id)
            ? CommandOutput.Call<DeferInboxRequest, DeferInboxResponse>(
                context,
                new DeferInboxRequest(id, context.Line.Value("reason"), context.Line.Value("until")),
                static r => $"deferred {r.Id.Value} until {r.Until.ToString("u", CultureInfo.InvariantCulture)}")
            : Task.FromResult(CommandOutput.Usage(context.Io, "the message id must not be blank", Entry.Spec.Usage));
}
