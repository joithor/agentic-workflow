using Aw.Meta.Core.Ports;
using Aw.Meta.Features.Usage;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Usage;

public sealed class UsageWaitTests
{
    private readonly UsageWorld _w = new();

    // Waiting is when the owner's window comes back: the hook runs at the n-th look.
    private sealed class OnWait(IDelay inner, Action hook) : IDelay
    {
        public ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
        {
            hook();
            return inner.Wait(duration, cancellationToken);
        }
    }

    private UsageWaitService Service(Action? onWait = null) =>
        new(_w.World.Config, _w.Reader, onWait is null ? _w.World.Delay : new OnWait(_w.World.Delay, onWait), _w.World.Clock);

    private async Task<UsageWaitResponse> Wait(bool untilOk = false, TimeSpan? max = null, Action? onWait = null)
    {
        var result = await Service(onWait).Handle(new UsageWaitRequest(untilOk, max ?? TimeSpan.FromHours(2)), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_limited_returns_at_once_with_ok()
    {
        await _w.Ready();

        var response = await Wait();

        Assert.True(response.Ok);
        Assert.Empty(_w.World.Delay.Waits);
        Assert.StartsWith("USAGE OK", Assert.Single(response.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_limited_lane_is_waited_for_until_its_reset_plus_the_margin_and_then_the_usage_ok_lines_print()
    {
        await _w.Ready();
        _w.LimitLane("api");
        _w.World.ClaudeTranscripts.Put(MetaWorld.MetaSessionId, UsageWorld.LimitLine + "\n", A.At(58));
        var start = _w.World.Clock.GetUtcNow();

        var response = await Wait();

        Assert.True(response.Ok);
        Assert.Equal(["USAGE OK api", "USAGE OK meta"], response.Lines.Where(l => !l.Contains(':', StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.True(_w.World.Clock.GetUtcNow() >= start.AddMinutes(32));
        Assert.True(_w.World.Clock.GetUtcNow() < start.AddMinutes(34));
        Assert.True(response.WaitedSeconds >= 32 * 60);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_wait_is_bounded_and_says_who_is_still_limited()
    {
        await _w.Ready();
        _w.LimitLane("api");

        var response = await Wait(max: TimeSpan.FromMinutes(10));

        Assert.False(response.Ok);
        Assert.Equal("USAGE WAIT TIMEOUT after 10m: still limited: api", response.Lines[^1]);
        Assert.True(_w.World.Clock.GetUtcNow() <= A.At(60).AddMinutes(11));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_that_recovers_before_its_reset_ends_the_wait()
    {
        await _w.Ready();
        _w.LimitLane("api");
        var waits = 0;

        var response = await Wait(onWait: () =>
        {
            if (++waits == 2)
            {
                _w.World.Panes.Show("t1-api", UsageWorld.Idle("⏺ working again"));
            }
        });

        Assert.True(response.Ok);
        Assert.True(_w.World.Clock.GetUtcNow() < A.At(60).AddMinutes(10));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_that_turns_unreadable_is_not_taken_for_recovered()
    {
        await _w.Ready();
        _w.LimitLane("api");
        var waits = 0;

        var response = await Wait(max: TimeSpan.FromMinutes(10), onWait: () =>
        {
            if (++waits == 1)
            {
                _w.World.Panes.Remove("t1-api");
            }
        });

        Assert.False(response.Ok);
        Assert.Contains("USAGE WAIT TIMEOUT", response.Lines[^1], StringComparison.Ordinal);
        Assert.Contains("api", response.Lines[^1], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Until_ok_keeps_waiting_past_the_reset_while_the_lane_still_shows_the_stop()
    {
        await _w.Ready();
        _w.LimitLane("api");
        var clock = _w.World.Clock;

        var response = await Wait(untilOk: true, max: TimeSpan.FromMinutes(90), onWait: () =>
        {
            if (clock.GetUtcNow() > A.At(60).AddMinutes(50))
            {
                _w.World.Panes.Show("t1-api", UsageWorld.Idle("⏺ resumed"));
            }
        });

        Assert.True(response.Ok);
        Assert.True(clock.GetUtcNow() > A.At(60).AddMinutes(50));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_until_ok_the_reset_time_ends_the_wait_even_if_the_stop_is_still_on_screen()
    {
        await _w.Ready();
        _w.LimitLane("api");

        var response = await Wait(untilOk: false);

        Assert.True(response.Ok);
        Assert.True(_w.World.Clock.GetUtcNow() < A.At(60).AddMinutes(40));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData((8 * 24 * 60), true)]
    [InlineData(((8 * 24 * 60) + 1), false)]
    public void Max_is_between_a_minute_and_eight_days(int minutes, bool valid)
    {
        var result = new UsageWaitValidator().Validate(new UsageWaitRequest(UntilOk: false, TimeSpan.FromMinutes(minutes)));

        Assert.Equal(valid, result.IsSuccess);
    }
}
