using Aw.Kernel.Json;
using Aw.Meta.Core.Config;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Config;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Config;

public sealed class UsageConfigTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_usage_monitor_defaults_to_a_two_minute_margin_a_fifteen_minute_probe_and_auto_resume()
    {
        var team = new TeamConfig("t1", "/work", ["meta", "api"], "meta");

        Assert.Equal((120, 15, true), (team.Usage.ResumeMarginSeconds, team.Usage.ProbeMinutes, team.Usage.AutoResume));
        Assert.Null(ThresholdChecks.Problem(team));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_margin_may_be_zero_and_the_probe_must_be_a_minute_or_more()
    {
        var team = new TeamConfig("t1", "/work", ["meta", "api"], "meta");

        Assert.Null(ThresholdChecks.Problem(team with { Usage = new UsageConfig(ResumeMarginSeconds: 0) }));
        Assert.Contains("usage.resumeMarginSeconds", ThresholdChecks.Problem(team with { Usage = new UsageConfig(ResumeMarginSeconds: -1) }), StringComparison.Ordinal);
        Assert.Contains("usage.probeMinutes", ThresholdChecks.Problem(team with { Usage = new UsageConfig(ProbeMinutes: 0) }), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_keys_are_read_from_the_team_file_by_name_and_a_bad_one_is_refused_at_load()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "team.json");
        var config = new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default));
        await File.WriteAllTextAsync(path, """{"team":"t1","repo":"/w","lanes":["meta"],"usage":{"resumeMarginSeconds":30,"probeMinutes":5,"autoResume":false}}""", TestContext.Current.CancellationToken);

        var loaded = (await config.Load(CancellationToken.None)).Value;

        Assert.Equal((30, 5, false), (loaded.Usage.ResumeMarginSeconds, loaded.Usage.ProbeMinutes, loaded.Usage.AutoResume));

        await File.WriteAllTextAsync(path, """{"team":"t1","repo":"/w","lanes":["meta"],"usage":{"probeMinutes":0}}""", TestContext.Current.CancellationToken);

        Assert.True((await config.Load(CancellationToken.None)).IsError);
    }
}
