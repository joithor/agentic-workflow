using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Cli;

public sealed class OutboxRecordCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private string Transcript => _cli.File("meta.jsonl");

    private static string Hook(string session, string to, string message) =>
        $$$"""{"session_id":"{{{session}}}","tool_name":"SendMessage","tool_input":{"to":"{{{to}}}","message":"{{{message}}}"},"tool_response":{"success":true}}""";

    [Fact, Trait("Lane", "integration")]
    public async Task A_recorded_reply_releases_the_stop_that_was_blocked()
    {
        _ = await _cli.Run("team-init", "--team", "sindri", "--repo", "/r", "--lanes", "api");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "sindri-meta");
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "sindri-api", "please rebase"), TestContext.Current.CancellationToken);
        var blocked = await _cli.Run("stop-check", "--session", "s-meta", "--transcript", Transcript);
        _cli.Clock.Advance(TimeSpan.FromMinutes(1));

        var other = await _cli.RunWithInput(Hook("s-other", "sindri-api", "hi"), "outbox-record");
        var recorded = await _cli.RunWithInput(Hook("s-meta", "sindri-api", "rebased"), "outbox-record");
        var released = await _cli.Run("stop-check", "--session", "s-meta", "--transcript", Transcript);

        Assert.Equal(1, blocked.Exit);
        Assert.Equal((0, string.Empty), (other.Exit, other.Out));
        Assert.Equal(0, recorded.Exit);
        Assert.Contains("closed: reply to sindri-api (m1)", recorded.Out, StringComparison.Ordinal);
        Assert.Equal((0, string.Empty), (released.Exit, released.Out));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Another_tool_call_is_ignored_and_a_payload_that_is_not_json_is_a_tool_error()
    {
        var bash = await _cli.RunWithInput("""{"session_id":"s","tool_name":"Bash","tool_input":{"command":"ls"}}""", "outbox-record");
        var junk = await _cli.RunWithInput("nope", "outbox-record");

        Assert.Equal((0, string.Empty), (bash.Exit, bash.Out));
        Assert.Equal(2, junk.Exit);
        Assert.StartsWith("AW-META-001: ", junk.Err, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
