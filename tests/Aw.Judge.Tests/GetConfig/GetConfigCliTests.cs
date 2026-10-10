namespace Aw.Judge.Tests.GetConfig;

// The contract of 'judge config get' (TS runConfigGet: the effective config as JSON, exit 0).
public sealed class GetConfigCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Config_get_prints_the_defaults_without_a_config_file()
    {
        var run = await new JudgeCli(_state).Run("config", "get");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var json = run.Json();
        var wakeGate = json.GetProperty("questions").GetProperty("wake-gate");
        Assert.True(wakeGate.GetProperty("enabled").GetBoolean());
        Assert.Equal(0.7, wakeGate.GetProperty("threshold").GetDouble());
        Assert.False(json.TryGetProperty("providers", out _));
        Assert.False(json.TryGetProperty("promptSort", out _));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_get_prints_the_file_s_questions_and_blocks()
    {
        Directory.CreateDirectory(Path.Combine(_state.Path, "judge"));
        await File.WriteAllTextAsync(
            Path.Combine(_state.Path, "judge", "config.json"),
            """{"questions":{"ask-check":{"enabled":false,"threshold":0.6,"providers":["jev"]}},"providers":{"agentClis":["codex-cli"],"jev":false},"promptSort":{"budgetMs":5000}}""",
            TestContext.Current.CancellationToken);

        var json = (await new JudgeCli(_state).Run("config", "get")).Json();

        var askCheck = json.GetProperty("questions").GetProperty("ask-check");
        Assert.False(askCheck.GetProperty("enabled").GetBoolean());
        Assert.Equal("jev", askCheck.GetProperty("providers")[0].GetString());
        Assert.Equal("codex-cli", json.GetProperty("providers").GetProperty("agentClis")[0].GetString());
        Assert.False(json.GetProperty("providers").GetProperty("jev").GetBoolean());
        Assert.Equal(1050, json.GetProperty("promptSort").GetProperty("budgetMs").GetDouble());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_config_subcommand_prints_the_config_usage() =>
        Assert.Equal(
            new CliRun(1, "", "usage: judge config get|set <question> <enabled|threshold> <value>\n"),
            await new JudgeCli(_state).Run("config", "list"));

    public void Dispose() => _state.Dispose();
}
