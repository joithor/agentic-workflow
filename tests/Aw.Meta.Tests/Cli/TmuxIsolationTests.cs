namespace Aw.Meta.Tests.Cli;

/// <summary>No aw-meta test can reach the real tmux server: the CLI test host points AW_TMUX at a fail-closed stub.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TmuxIsolationTests : IDisposable
{
    private readonly CliHarness _cli = new();

    [Fact, Trait("Lane", "integration")]
    public async Task The_CLI_test_hosts_tmux_is_the_fail_closed_stub_under_its_temp_directory()
    {
        var script = await File.ReadAllTextAsync(_cli.Tmux, TestContext.Current.CancellationToken);

        Assert.StartsWith(_cli.File(string.Empty), _cli.Tmux, StringComparison.Ordinal);
        Assert.Contains(FailClosedTmux.Marker, script, StringComparison.Ordinal);
        Assert.Contains("exit 1", script, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_test_cannot_point_AW_TMUX_at_a_program_outside_its_temp_directory()
    {
        _ = Assert.Throws<InvalidOperationException>(() => _cli.SetEnv("AW_TMUX", "/usr/bin/tmux"));
        _ = Assert.Throws<InvalidOperationException>(() => _cli.SetEnv("AW_TMUX", _cli.File("../tmux")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Health_and_lanes_with_a_non_Claude_lane_call_the_stub_and_the_lane_reads_as_down()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "meta,api");
        var sessions = Directory.CreateDirectory(_cli.File("meta/teams/t1/lane-sessions")).FullName;
        await File.WriteAllTextAsync(Path.Combine(sessions, "api"), "s-1\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sessions, "api.agent"), """{"agent":"codex","processName":"codex","hooks":true}""", TestContext.Current.CancellationToken);

        var lanes = await _cli.Run("lanes");
        var health = await _cli.Run("health");

        var log = await File.ReadAllTextAsync(_cli.File("tmux-stub.log"), TestContext.Current.CancellationToken);
        Assert.Contains("display-message", log, StringComparison.Ordinal);
        Assert.Contains("api          (no session)", lanes.Out, StringComparison.Ordinal);
        Assert.Contains("lanes with no live session: api", health.Out, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
