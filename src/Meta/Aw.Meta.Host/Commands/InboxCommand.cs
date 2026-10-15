using Aw.Meta.Features.Inbox;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'inbox' command.</summary>
internal static class InboxCommand
{
    internal static CommandEntry Entry { get; } = new(
        "inbox",
        new CommandSpec("inbox [--all] [--json]", [], [], ["all"]),
        NeedsConfig: true,
        static context => CommandOutput.Call<ListInboxRequest, ListInboxResponse>(context, new ListInboxRequest(context.Line.Has("all")), Text));

    private static string Text(ListInboxResponse response) =>
        response.Items.Count == 0
            ? "nothing in the inbox"
            : string.Join(Environment.NewLine, response.Items.Select(Line));

    private static string Line(InboxItem item) =>
        $"{item.Id.Value}  {item.From}  {item.Kind}  {item.State}{(item.Until is { } until ? " until " + until.ToString("u", System.Globalization.CultureInfo.InvariantCulture) : string.Empty)}  {item.Body}";
}
