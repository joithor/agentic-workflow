using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Usage;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Usage;

public sealed class UsageTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_timed_call_records_its_elapsed_time_and_reported_usage()
    {
        var clock = new FakeTimeProvider();
        var log = new InvocationLog();

        var answer = await log.TimeAsync(InvocationPhase.VisualCritique, clock, () =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            return ValueTask.FromResult<string?>("ok");
        }, static _ => new InvocationUsage("haiku", 10, 2));

        Assert.Equal("ok", answer);
        Assert.Equal(new ModelInvocation(InvocationPhase.VisualCritique, "haiku", 1500, Ok: true, 10, 2, CacheHit: false), Assert.Single(log.All));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_without_an_answer_records_not_ok_with_unknown_tokens()
    {
        var log = new InvocationLog();

        var answer = await log.TimeAsync(InvocationPhase.SelectorRepair, new FakeTimeProvider(), static () => ValueTask.FromResult<string?>(null), static _ => new InvocationUsage("m", 1, 1));

        Assert.Null(answer);
        var invocation = Assert.Single(log.All);
        Assert.False(invocation.Ok);
        Assert.Null(invocation.InputTokens);
        Assert.Null(invocation.Model);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_planning_block_is_recorded_as_given_with_unknowns_left_null()
    {
        var log = new InvocationLog();

        log.RecordPlanning(new PlanningMeta(Model: "opus", ElapsedMs: 42));

        Assert.Equal(new ModelInvocation(InvocationPhase.Planning, "opus", 42, Ok: true, InputTokens: null, OutputTokens: null, CacheHit: false), log.All[0]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.7339.16 Safari/537.36")]
    [InlineData("  ", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.7339.16 Safari/537.36")]
    [InlineData("custom-agent", "custom-agent")]
    public void The_session_presents_as_desktop_chrome_unless_overridden(string? overrideValue, string expected) =>
        Assert.Equal(expected, UserAgent.For("140.0.7339.16", overrideValue));
}
