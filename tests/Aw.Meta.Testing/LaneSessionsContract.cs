using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ILaneSessions contract, run against the in-memory fake and the pane-process-tree adapter.</summary>
public abstract class LaneSessionsContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh reader plus a way to start a Claude in a lane's pane.</summary>
    /// <returns>The reader and the starter (tmux session, how many processes below the pane's own the Claude runs, the content of its sessions record).</returns>
    protected abstract (ILaneSessions Sessions, Action<string, int, string> Start) Create();

    /// <summary>A session nothing runs in has no current id.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_session_with_nothing_running_has_no_current_id()
    {
        var (sessions, _) = Create();

        var found = await sessions.Current("t1-api", _none);

        Expect.That(found.IsSuccess && found.Value is null, "null");
    }

    /// <summary>The Claude may be the pane's process or any descendant of it; its record gives the id.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_id_of_a_claude_under_the_pane_is_found_at_any_depth()
    {
        var (sessions, start) = Create();
        start("t1-api", 0, "{\"sessionId\":\"s-top\"}");
        start("t1-ui", 2, "{\"pid\":1,\"sessionId\":\"s-grandchild\",\"cwd\":\"/x\"}");

        var api = await sessions.Current("t1-api", _none);
        var ui = await sessions.Current("t1-ui", _none);

        Expect.That(api.Value?.Value == "s-top", "the pane's own process");
        Expect.That(ui.Value?.Value == "s-grandchild", "a grandchild of the pane's process");
    }

    /// <summary>A record that is not JSON, has no id or an empty one answers null, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_malformed_or_empty_record_answers_null()
    {
        var (sessions, start) = Create();
        start("t1-a", 1, "{ not json");
        start("t1-b", 1, "{\"sessionId\":\"\"}");
        start("t1-c", 1, "{\"pid\":3}");
        start("t1-d", 1, "[1,2]");

        foreach (var session in new[] { "t1-a", "t1-b", "t1-c", "t1-d" })
        {
            var found = await sessions.Current(session, _none);
            Expect.That(found.IsSuccess && found.Value is null, session + " answers null");
        }
    }
}
