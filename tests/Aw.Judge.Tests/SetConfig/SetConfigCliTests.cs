namespace Aw.Judge.Tests.SetConfig;

// The contract of 'judge config set' (TS runConfigSet): prints the new config, keeps hand-edited blocks.
public sealed class SetConfigCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    private string ConfigFile => Path.Combine(_state.Path, "judge", "config.json");

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_keeps_a_hand_edited_providers_and_prompt_sort_block()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);   // why: the path has a directory part by construction
        await File.WriteAllTextAsync(ConfigFile,
            """{"questions":{},"providers":{"agentClis":["cursor-cli"],"jev":false},"promptSort":{"enabled":false,"scaffolds":{"brief":true}}}""",
            TestContext.Current.CancellationToken);
        var cli = new JudgeCli(_state);

        var run = await cli.Run("config", "set", "ask-check", "threshold", "0.6");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var json = (await cli.Run("config", "get")).Json();
        Assert.Equal(0.6, json.GetProperty("questions").GetProperty("ask-check").GetProperty("threshold").GetDouble());
        Assert.True(json.GetProperty("questions").GetProperty("ask-check").GetProperty("enabled").GetBoolean());
        Assert.Equal("cursor-cli", json.GetProperty("providers").GetProperty("agentClis")[0].GetString());
        Assert.False(json.GetProperty("providers").GetProperty("jev").GetBoolean());
        Assert.False(json.GetProperty("promptSort").GetProperty("enabled").GetBoolean());
        Assert.True(json.GetProperty("promptSort").GetProperty("scaffolds").GetProperty("brief").GetBoolean());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_enabled_prints_and_stores_the_new_config()
    {
        var cli = new JudgeCli(_state);

        var run = await cli.Run("config", "set", "wake-gate", "enabled", "false");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        Assert.False(run.Json().GetProperty("questions").GetProperty("wake-gate").GetProperty("enabled").GetBoolean());
        Assert.True(File.Exists(ConfigFile));
        Assert.False((await cli.Run("config", "get")).Json().GetProperty("questions").GetProperty("wake-gate").GetProperty("enabled").GetBoolean());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_refuses_a_threshold_that_isnt_a_number()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "not a number: high\n"), await cli.Run("config", "set", "wake-gate", "threshold", "high"));
        Assert.False(File.Exists(ConfigFile));
    }

    public void Dispose() => _state.Dispose();
}
