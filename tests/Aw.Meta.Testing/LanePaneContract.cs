using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ILanePane contract, run against the in-memory fake and the tmux adapter (over a scripted command runner, never a real tmux).</summary>
public abstract class LanePaneContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A pane with no session, plus a way to start one the way the launcher does.</summary>
    /// <returns>The pane and the starter (session, the command running in it, clients attached).</returns>
    protected abstract ValueTask<(ILanePane Pane, Action<string, string, int> Start)> Create();

    /// <summary>A session that does not exist probes as null, not as an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_session_probes_as_null()
    {
        var (pane, _) = await Create();

        var state = await pane.Probe("t1-api", _none);

        Expect.That(state.IsSuccess && state.Value is null, "no such session is a null state");
    }

    /// <summary>A session reports the command in its pane and how many clients are attached, and only the exact session answers.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_session_reports_its_command_and_its_clients_for_that_exact_name_only()
    {
        var (pane, start) = await Create();
        start("t1-api", "codex", 2);

        var state = await pane.Probe("t1-api", _none);
        var prefix = await pane.Probe("t1-ap", _none);
        var longer = await pane.Probe("t1-api-2", _none);

        Expect.That(state.Value is { Clients: 2 } found && string.Equals(found.Command, "codex", StringComparison.Ordinal), "command and clients");
        Expect.That(prefix.Value is null && longer.Value is null, "a prefix or an extension of the name is another session");
    }

    /// <summary>A wake of a live session succeeds; a wake of a missing one fails.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_wake_succeeds_for_a_live_session_and_fails_for_a_missing_one()
    {
        var (pane, start) = await Create();
        start("t1-api", "codex", 0);

        var live = await pane.Wake("t1-api", "hello", _none);
        var missing = await pane.Wake("t1-ui", "hello", _none);

        Expect.That(live.IsSuccess, "the live session is woken");
        Expect.That(missing.IsError, "a missing session can't be woken");
    }

    /// <summary>The pid of the pane's process is read for a live session and is null for a missing one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_pane_pid_is_read_for_a_live_session_and_null_for_a_missing_one()
    {
        var (pane, start) = await Create();
        start("t1-api", "claude", 0);

        var live = await pane.PanePid("t1-api", _none);
        var missing = await pane.PanePid("t1-ui", _none);

        Expect.That(live.IsSuccess && live.Value == 4242, "the pid the pane started");
        Expect.That(missing.IsSuccess && missing.Value is null, "no pid for a missing session");
    }

    /// <summary>Typing, pressing Enter and reading the pane work for a live session and fail for a missing one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Type_press_enter_and_capture_work_for_a_live_session_and_fail_for_a_missing_one()
    {
        var (pane, start) = await Create();
        start("t1-api", "claude", 0);

        var typed = await pane.Type("t1-api", "hello", _none);
        var entered = await pane.PressEnter("t1-api", _none);
        var shown = await pane.Capture("t1-api", _none);

        Expect.That(typed.IsSuccess && entered.IsSuccess && shown.IsSuccess, "a live session answers");
        Expect.That((await pane.Type("t1-ui", "hello", _none)).IsError, "type fails for a missing session");
        Expect.That((await pane.PressEnter("t1-ui", _none)).IsError, "enter fails for a missing session");
        Expect.That((await pane.Capture("t1-ui", _none)).IsError, "capture fails for a missing session");
    }
}
