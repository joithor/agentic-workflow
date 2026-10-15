using Aw.Meta.Core.Ids;
using Aw.Meta.Features.InboxDismiss;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'inbox-dismiss' command.</summary>
internal static class InboxDismissCommand
{
    internal static CommandEntry Entry { get; } = new(
        "inbox-dismiss",
        new CommandSpec("inbox-dismiss <id> --reason <text> [--json]", ["reason"], [], [], Positionals: 1),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context) =>
        MessageId.TryFrom(context.Line.Positional(0), out var id)
            ? CommandOutput.Call<DismissInboxRequest, DismissInboxResponse>(context, new DismissInboxRequest(id, context.Line.Value("reason")), static r => $"dismissed {r.Id.Value}")
            : Task.FromResult(CommandOutput.Usage(context.Io, "the message id must not be blank", Entry.Spec.Usage));
}
