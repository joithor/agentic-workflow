using Aw.Judge.Testing;
using static Aw.Judge.Tests.AskQuestion.AskQuestionHarness;

namespace Aw.Judge.Tests.AskQuestion;

// The providers allowlist: '--providers a,b' anywhere in argv, else AW_JUDGE_PROVIDERS; rules is always kept, and a
// list the judge can't read is a usage error for any command.
public sealed class ProvidersAllowlistCliTests : IDisposable
{
    private const string Progress = """{"text":"progress on the parser, half done","senderKind":"teammate"}""";

    private readonly TempStateDirectory _state = new();

    private JudgeCli Judge(ScriptedProvider provider, string? variable = null) => new(_state)
    {
        Providers = [provider],
        Environment = variable is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_PROVIDERS"] = variable },
    };

    [Theory, Trait("Lane", "integration")]
    [InlineData("--providers", "rules", "wake-gate")]
    [InlineData("wake-gate", "--providers", "rules")]
    public async Task The_flag_anywhere_in_argv_limits_the_chain(params string[] args)
    {
        var claude = Cli().Decides("batch", 0.9);

        var run = await Judge(claude).RunWithInput(Progress, args);

        Assert.Equal((2, "no-provider-decided"), (run.ExitCode, run.Json().GetProperty("reasonCode").GetString()));
        Assert.Empty(claude.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task AW_JUDGE_PROVIDERS_limits_the_chain_when_the_flag_is_absent()
    {
        var claude = Cli().Decides("batch", 0.9);

        var run = await Judge(claude, variable: "rules").RunWithInput(Progress, "wake-gate");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(claude.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_flag_wins_over_AW_JUDGE_PROVIDERS()
    {
        var run = await Judge(Cli().Decides("batch", 0.9), variable: "rules").RunWithInput(Progress, "--providers", "claude-cli", "wake-gate");

        Assert.Equal((0, "batch"), (run.ExitCode, run.Json().GetProperty("decision").GetString()));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(null, "--providers", "bogus", "wake-gate")]
    [InlineData(null, "wake-gate", "--providers")]
    [InlineData("bogus", "wake-gate")]
    [InlineData(",", "health")]
    public async Task A_list_the_judge_cant_read_is_a_usage_error(string? variable, params string[] args)
    {
        var claude = Cli().Decides("batch", 0.9);

        var run = await Judge(claude, variable).RunWithInput(Progress, args);

        Assert.Equal((64, ""), (run.ExitCode, run.Stdout));
        Assert.StartsWith("usage: judge ", run.Stderr, StringComparison.Ordinal);
        Assert.Empty(claude.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Other_commands_take_the_flag_too()
    {
        var run = await new JudgeCli(_state).Run("health", "--providers", "jev");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("ok", run.Json().GetProperty("status").GetString());
    }

    public void Dispose() => _state.Dispose();
}
