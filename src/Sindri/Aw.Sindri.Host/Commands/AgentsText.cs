using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.ListSessions;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri agents' and 'sindri agents show' as text (collector spec CI-8): a state is a word, never a colour.</summary>
internal static class AgentsText
{
    /// <summary>The list: a head, one line per session, the recently ended ones, then one line per problem.</summary>
    /// <param name="list">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(SessionList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }
        else
        {
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{list.Counts.Live} live · {list.Counts.Asking} asking · {list.Counts.OutsideWorkflow} outside a workflow"));
            foreach (var row in list.Sessions)
            {
                _ = text.AppendLine(Line(row));
            }
        }

        foreach (var ended in list.RecentlyEnded)
        {
            var resume = ended.Resume is { Count: > 0 } argv ? "  resume: " + string.Join(' ', argv) : string.Empty;
            _ = text.AppendLine($"ended  {ended.Session.Ref}  {Time(ended.EndedAt)}{resume}");
        }

        foreach (var problem in list.Problems)
        {
            _ = text.AppendLine($"problem {problem.Code} ({problem.Source}): {problem.Text}");
        }

        return text.ToString();
    }

    /// <summary>One session in full.</summary>
    /// <param name="view">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(SessionView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var text = new StringBuilder();
        _ = text.AppendLine($"{view.Session.Ref}  {view.Session.ProviderLabel}");
        Fields(text, view);
        foreach (var entry in view.StateHistory ?? [])
        {
            _ = text.AppendLine($"  {Time(entry.At)}  {entry.State.ToString().ToLowerInvariant()}{(entry.Reason is { } reason ? " · " + reason.ToString().ToLowerInvariant() : string.Empty)}");
        }

        return text.ToString();
    }

    private static void Fields(StringBuilder text, SessionView view)
    {
        var row = view.Session;
        Field(text, "state", State(row) + (row.Estimated ? " (estimated)" : string.Empty) + " since " + Time(row.Since) + " from " + row.StateSource.ToString().ToLowerInvariant());
        Field(text, "repo", row.Repo?.Value);
        Field(text, "worktree", row.Worktree);
        Field(text, "host", row.Host.Label + (row.Host.SocketKind is { } kind ? $" (tmux socket: {kind})" : string.Empty));
        Field(text, "tmux", row.Host.Session is { } session ? $"{session} {row.Host.Pane}" : null);
        Field(text, "lane", row.Lane is { } lane ? $"{lane.Team}/{lane.Lane}" : null);
        Field(text, "launch", row.LaunchId?.Value);
        Field(text, "started", row.StartedAt is { } started ? Time(started) : null);
        Field(text, "title", row.Title);
        Field(text, "pid", row.Pid?.ToString(CultureInfo.InvariantCulture));
        Field(text, "cwd", view.Cwd);
        Field(text, "socket", row.Host.Socket);
    }

    private static string Line(SessionRow row) =>
        $"{row.Ref}  {State(row)}  {row.Repo?.Value ?? "-"}  {row.Host.Label}{(row.Estimated ? "  (estimated)" : string.Empty)}{(row.Title is { } title ? "  " + title : string.Empty)}";

    private static string State(SessionRow row) =>
        row.State == SessionState.Asking && row.Asking is { } asking
            ? "ASKING · " + asking.Reason.ToString().ToLowerInvariant() + (asking.Tool is { } tool ? " · " + tool : string.Empty)
            : row.State.ToString().ToUpperInvariant();

    private static void Field(StringBuilder text, string name, string? value)
    {
        if (value is not null)
        {
            _ = text.AppendLine($"{name + ":",-10}{value}");
        }
    }

    private static string Time(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
