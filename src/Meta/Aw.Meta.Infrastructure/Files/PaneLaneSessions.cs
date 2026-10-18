using System.Text.Json;
using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// ILaneSessions from the pane's process tree: the pane's pid and its descendants are looked up in Claude Code's sessions directory
/// (&lt;pid&gt;.json holds the current "sessionId"). Best effort: anything that can't be read answers null.
/// </summary>
/// <param name="pane">The lanes' tmux sessions.</param>
/// <param name="processes">The process table.</param>
/// <param name="directory">Claude Code's sessions directory.</param>
internal sealed class PaneLaneSessions(ILanePane pane, IProcessTable processes, string directory) : ILaneSessions
{
    public async ValueTask<Result<SessionId?>> Current(string session, CancellationToken cancellationToken)
    {
        var root = await pane.PanePid(session, cancellationToken).ConfigureAwait(false);
        if (root.IsError || root.Value is not { } start)
        {
            return Results.Success<SessionId?>(value: null);
        }

        var table = await processes.Snapshot(cancellationToken).ConfigureAwait(false);
        if (table.IsError)
        {
            return Results.Success<SessionId?>(value: null);
        }

        var queue = new Queue<int>([start]);
        var seen = new HashSet<int>();
        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            if (!seen.Add(pid))
            {
                continue;
            }

            if (await Read(pid, cancellationToken).ConfigureAwait(false) is { } id)
            {
                return Results.Success<SessionId?>(id);
            }

            foreach (var child in table.Value.Processes.Where(p => p.Ppid == pid))
            {
                queue.Enqueue(child.Pid);
            }
        }

        return Results.Success<SessionId?>(value: null);
    }

    private async ValueTask<SessionId?> Read(int pid, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(directory, pid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("sessionId", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 } text
                ? SessionId.From(text)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
