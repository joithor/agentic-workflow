using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ProvidersAllowlistTests
{
    [Fact]
    public void Neither_flag_nor_environment_allows_everything_and_keeps_argv()
    {
        var parsed = ProvidersAllowlist.Parse(["why", "d1"], environmentValue: null);

        Assert.Null(parsed.Value.Allowed);
        Assert.Equal(["why", "d1"], parsed.Value.Arguments);
        Assert.True(parsed.Value.IsAllowed(ProviderName.Jev));
    }

    [Fact]
    public void The_flag_is_read_and_removed_wherever_it_appears_and_wins_over_the_environment()
    {
        var parsed = ProvidersAllowlist.Parse(["--providers", "jev, claude-cli,jev", "ask-check"], "rules");

        Assert.Equal([ProviderName.Jev, ProviderName.ClaudeCli], parsed.Value.Allowed);
        Assert.Equal(["ask-check"], parsed.Value.Arguments);
        Assert.False(parsed.Value.IsAllowed(ProviderName.CodexCli));
    }

    [Fact]
    public void The_environment_is_used_when_the_flag_is_absent()
    {
        var parsed = ProvidersAllowlist.Parse(["health"], "jev");

        Assert.Equal([ProviderName.Jev], parsed.Value.Allowed);
        Assert.Equal(["health"], parsed.Value.Arguments);
    }

    [Theory]
    [InlineData(new[] { "--providers" }, null, "--providers needs a comma-separated list")]
    [InlineData(new[] { "--providers", "" }, null, "--providers list is empty")]
    [InlineData(new[] { "health" }, " , ", "--providers list is empty")]
    [InlineData(new[] { "--providers", "jev,gpt,Rules" }, null, "unknown provider(s): gpt, Rules")]
    public void A_malformed_list_is_an_error_with_the_ts_message(string[] arguments, string? environment, string message)
    {
        var parsed = ProvidersAllowlist.Parse(arguments, environment);

        Assert.True(parsed.IsError);
        Assert.Equal((JudgeErrors.ProvidersFlag.Code, message), (parsed.Error.Code, parsed.Error.Message));
    }
}
