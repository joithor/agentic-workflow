using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class TmuxLanePaneTests
{
    private readonly InMemoryCommandRunner _runner = new();

    private TmuxLanePane Pane() => new(_runner, "tmux");

    [Fact, Trait("Lane", "unit")]
    public async Task The_probe_reads_the_pane_command_and_counts_attached_clients_on_the_exact_session()
    {
        _ = _runner.When("display-message", "codex\n").When("list-clients", "x\nx\n");

        var state = await Pane().Probe("sindri-api", CancellationToken.None);

        Assert.Equal(("codex", 2), (state.Value!.Command, state.Value.Clients));
        Assert.Contains(_runner.Lines, l => string.Equals(l, "tmux display-message -p -t =sindri-api: #{pane_current_command}", StringComparison.Ordinal));
        Assert.Contains(_runner.Lines, l => string.Equals(l, "tmux list-clients -t =sindri-api -F x", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_session_probes_as_null()
    {
        _ = _runner.When("display-message", exitCode: 1);

        Assert.Null((await Pane().Probe("sindri-api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_wake_types_the_line_literally_to_the_exact_session_then_presses_Enter()
    {
        _ = _runner.When("send-keys");

        var result = await Pane().Wake("sindri-api", "hello -x; $(date)", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["tmux send-keys -t =sindri-api: -l hello -x; $(date)", "tmux send-keys -t =sindri-api: Enter"], _runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_send_keys_is_an_error_and_Enter_is_not_sent()
    {
        _ = _runner.When("send-keys", exitCode: 1);

        var result = await Pane().Wake("sindri-api", "hi", CancellationToken.None);

        Assert.True(result.IsError);
        _ = Assert.Single(_runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_a_socket_every_tmux_call_names_it_with_dash_L_and_without_one_none_does()
    {
        _ = _runner.When("display-message", "codex\n").When("list-clients").When("send-keys");
        var withSocket = new TmuxLanePane(_runner, "tmux", "t1-socket");

        _ = await withSocket.Probe("t1-api", CancellationToken.None);
        _ = await withSocket.Wake("t1-api", "hi", CancellationToken.None);
        var plain = new InMemoryCommandRunner().When("display-message", "codex\n").When("list-clients");
        _ = await new TmuxLanePane(plain, "tmux").Probe("t1-api", CancellationToken.None);

        Assert.All(_runner.Lines, l => Assert.StartsWith("tmux -L t1-socket ", l, StringComparison.Ordinal));
        Assert.Equal(4, _runner.Lines.Count);
        Assert.DoesNotContain(plain.Lines, l => l.Contains("-L", StringComparison.Ordinal));
    }
}
