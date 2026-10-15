using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Cli;

public sealed class StopCheckCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private string Transcript => _cli.File("meta.jsonl");

    private async Task Ready()
    {
        _ = await _cli.Run("team-init", "--team", "sindri", "--repo", "/r", "--lanes", "api,ui");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "sindri-meta");
    }

    private Task<CliResult> Stop(string session = "s-meta", params string[] extra) =>
        _cli.Run(["stop-check", "--session", session, "--transcript", Transcript, .. extra]);

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_that_is_not_the_meta_stops_silently_even_with_no_team_config()
    {
        var result = await Stop("s-other");

        Assert.Equal((0, string.Empty, string.Empty), (result.Exit, result.Out, result.Err));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_meta_is_blocked_with_exit_1_and_the_next_command_while_a_peer_is_unanswered()
    {
        await Ready();
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "sindri-api", "please rebase"), TestContext.Current.CancellationToken);

        var blocked = await Stop();

        Assert.Equal(1, blocked.Exit);
        Assert.Contains("aw-meta: 1 open obligation(s)", blocked.Out, StringComparison.Ordinal);
        Assert.Contains("next: SendMessage to sindri-api; or aw-meta inbox-dismiss m1", blocked.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_loop_guard_limit_comes_from_AW_META_STOP_MAX_and_a_continuation_is_let_through_with_a_warning()
    {
        await Ready();
        _cli.SetEnv("AW_META_STOP_MAX", "2");
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "sindri-api", "stuck"), TestContext.Current.CancellationToken);

        var first = await Stop(extra: "--hook-active");
        var second = await Stop(extra: "--hook-active");
        var third = await Stop(extra: "--hook-active");

        Assert.Equal([1, 1, 0], [first.Exit, second.Exit, third.Exit]);
        Assert.StartsWith("aw-meta: stop allowed after 2 identical blocks", third.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_output_names_the_obligation_kind_as_a_string()
    {
        await Ready();
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "sindri-api", "x"), TestContext.Current.CancellationToken);

        var result = await Stop(extra: "--json");

        Assert.Equal(1, result.Exit);
        Assert.Contains("\"kind\":\"Reply\"", result.Out.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
