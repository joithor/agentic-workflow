namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class UsageCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private async Task Ready() => _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "meta,api");

    [Fact, Trait("Lane", "integration")]
    public async Task Usage_lists_each_lane_and_the_meta_as_unknown_when_nothing_can_be_read_and_exits_0()
    {
        await Ready();

        var result = await _cli.Run("usage");

        Assert.Equal(0, result.Exit);
        Assert.Contains("api", result.Out, StringComparison.Ordinal);
        Assert.Contains("meta", result.Out, StringComparison.Ordinal);
        Assert.Contains("unknown", result.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Usage_json_has_the_subjects_and_the_attention_flag()
    {
        await Ready();

        var result = await _cli.Run("usage", "--json");

        Assert.Equal(0, result.Exit);
        var compact = result.Out.Replace(" ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"subjects\":[", compact, StringComparison.Ordinal);
        Assert.Contains("\"attention\":false", compact, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"Unknown\"", compact, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Usage_wait_with_nothing_limited_prints_usage_ok_and_exits_0_at_once()
    {
        await Ready();

        var result = await _cli.Run("usage-wait", "--max", "2m");

        Assert.Equal(0, result.Exit);
        Assert.StartsWith("USAGE OK", result.Out, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("soon")]
    [InlineData("0")]
    public async Task Usage_wait_refuses_a_max_that_is_not_a_duration(string max)
    {
        await Ready();

        var result = await _cli.Run("usage-wait", "--max", max);

        Assert.Equal(2, result.Exit);
    }

    public void Dispose() => _cli.Dispose();
}
