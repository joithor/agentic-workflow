using Aw.Meta.Core.Config;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class TmuxLanePaneTests
{
    private readonly InMemoryCommandRunner _runner = new();

    private readonly InMemoryMetaConfig _config = new();

    private TmuxLanePane Pane() => new(_runner, "tmux", socket: null, _config);

    private async Task<TmuxLanePane> TeamPane()
    {
        _ = await _config.Save(new TeamConfig("t1", "/work", ["meta", "api"], "meta"), CancellationToken.None);
        return Pane();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_probe_reads_the_pane_command_and_counts_attached_clients_on_the_exact_session()
    {
        _ = _runner.When("display-message", "codex\n").When("list-clients", "x\nx\n");

        var state = await (await TeamPane()).Probe("t1-api", CancellationToken.None);

        Assert.Equal(("codex", 2), (state.Value!.Command, state.Value.Clients));
        Assert.Contains(_runner.Lines, l => string.Equals(l, "tmux -L t1 display-message -p -t =t1-api: #{pane_current_command}", StringComparison.Ordinal));
        Assert.Contains(_runner.Lines, l => string.Equals(l, "tmux -L t1 list-clients -t =t1-api -F x", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_session_probes_as_null()
    {
        _ = _runner.When("display-message", exitCode: 1);

        Assert.Null((await (await TeamPane()).Probe("t1-api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_wake_types_the_line_literally_to_the_exact_session_then_presses_Enter()
    {
        _ = _runner.When("send-keys");

        var result = await (await TeamPane()).Wake("t1-api", "hello -x; $(date)", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["tmux -L t1 send-keys -t =t1-api: -l hello -x; $(date)", "tmux -L t1 send-keys -t =t1-api: Enter"], _runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_send_keys_is_an_error_and_Enter_is_not_sent()
    {
        _ = _runner.When("send-keys", exitCode: 1);

        var result = await (await TeamPane()).Wake("t1-api", "hi", CancellationToken.None);

        Assert.True(result.IsError);
        _ = Assert.Single(_runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_tmux_call_names_the_socket_the_override_gives_and_without_one_the_team_name()
    {
        _ = _runner.When("display-message", "codex\n").When("list-clients").When("send-keys");
        var withSocket = new TmuxLanePane(_runner, "tmux", "t1-socket", _config);

        _ = await withSocket.Probe("t1-api", CancellationToken.None);
        _ = await withSocket.Wake("t1-api", "hi", CancellationToken.None);
        var team = new InMemoryCommandRunner().When("display-message", "codex\n").When("list-clients");
        _ = await _config.Save(new TeamConfig("t1", "/work", ["meta", "api"], "meta"), CancellationToken.None);
        var plain = new TmuxLanePane(team, "tmux", socket: null, _config);
        _ = await plain.Probe("t1-api", CancellationToken.None);

        Assert.All(_runner.Lines, l => Assert.StartsWith("tmux -L t1-socket ", l, StringComparison.Ordinal));
        Assert.Equal(4, _runner.Lines.Count);
        Assert.All(team.Lines, l => Assert.StartsWith("tmux -L t1 ", l, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_and_a_socket_the_lane_is_down_and_a_wake_is_an_error_and_no_tmux_runs()
    {
        var pane = Pane();

        Assert.Null((await pane.Probe("t1-api", CancellationToken.None)).Value);
        Assert.True((await pane.Wake("t1-api", "hi", CancellationToken.None)).IsError);
        Assert.Empty(_runner.Lines);
    }
}
