using System.Globalization;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.SyncInbox;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'inbox-sync' command.</summary>
internal static class SyncInboxCommand
{
    internal static CommandEntry Entry { get; } = new(
        "inbox-sync",
        new CommandSpec("inbox-sync --session <uuid> --transcript <path> [--json]", ["session", "transcript"], [], []),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        if (!SessionId.TryParse(context.Line.Value("session"), CultureInfo.InvariantCulture, out var session))
        {
            return Task.FromResult(CommandOutput.Usage(context.Io, "--session must not be blank", Entry.Spec.Usage));
        }

        return CommandOutput.Call<SyncInboxRequest, SyncInboxResponse>(context, new SyncInboxRequest(session, context.Line.Value("transcript")), Text);
    }

    private static string Text(SyncInboxResponse response) =>
        response.NewMessages.Count == 0
            ? "no new messages"
            : string.Create(CultureInfo.InvariantCulture, $"{response.NewMessages.Count} new message(s): ") + string.Join(", ", response.NewMessages.Select(id => id.Value));
}
