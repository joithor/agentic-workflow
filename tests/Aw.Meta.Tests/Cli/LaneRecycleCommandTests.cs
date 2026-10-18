namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LaneRecycleCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private async Task Ready()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "meta,api");
        var lanes = _cli.File("meta/teams/t1/lane-sessions");
        _ = Directory.CreateDirectory(lanes);
        await File.WriteAllTextAsync(Path.Combine(lanes, "api"), "s-old\n", TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_digest_is_refused_with_exit_1_and_AW_META_021()
    {
        await Ready();

        var result = await _cli.Run("lane-recycle", "api", "--digest", _cli.File("none.md"));

        Assert.Equal(1, result.Exit);
        Assert.Contains("AW-META-021", result.Err, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_lane_with_no_live_session_is_refused_even_if_lane_sessions_holds_an_id()
    {
        await Ready();
        var digest = _cli.File("digest.md");
        await File.WriteAllTextAsync(digest, "state", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(digest, _cli.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1));

        var result = await _cli.Run("lane-recycle", "api", "--digest", digest, "--dry-run");

        Assert.Equal(1, result.Exit);
        Assert.Contains("AW-META-021: lane api: no live Claude session", result.Err, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Lanes_context_reports_a_lane_with_no_transcript_as_unknown()
    {
        await Ready();

        var result = await _cli.Run("lanes-context", "--json");

        Assert.Equal(0, result.Exit);
        Assert.Contains("\"verdict\":\"Unknown\"", result.Out.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
