using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IProjectorStatus"/> honours (API spec §2.1, §2.2): a wait returns once the cursor
/// reaches its target, times out with SND-API-011 naming the cursor and the target, and fails at once while stalled;
/// a rebuild request shows at once.
/// </summary>
public abstract class ProjectorStatusContract
{
    private static TimeSpan Limit => TimeSpan.FromSeconds(2);

    /// <summary>The contract's clock: waits time out on it.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh status over <paramref name="clock"/>.</summary>
    /// <param name="clock">The only clock.</param>
    /// <returns>The subject.</returns>
    protected abstract ProjectorSubject Create(TimeProvider clock);

    /// <summary>A wait for a reached sequence returns at once; one ahead returns once the cursor gets there.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task WaitFor_returns_once_the_cursor_reaches_the_target()
    {
        var subject = Create(Clock);
        subject.Publish(Status(cursor: 3, head: 5));

        var reached = await subject.Status.WaitFor(3, Limit, Token);
        var pending = subject.Status.WaitFor(5, Limit, Token).AsTask();
        Expect.True(!pending.IsCompleted);
        subject.Publish(Status(cursor: 5, head: 5));

        Expect.True(reached.IsSuccess);
        Expect.True((await pending).IsSuccess);
        Expect.Equal(5L, subject.Status.Current.Cursor);
    }

    /// <summary>Past the limit the wait fails with SND-API-011, naming the cursor and the target.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task WaitFor_times_out_with_SND_API_011_naming_cursor_and_target()
    {
        var subject = Create(Clock);
        subject.Publish(Status(cursor: 3, head: 10));

        var waiting = subject.Status.WaitFor(10, Limit, Token).AsTask();
        Clock.Advance(Limit);
        var result = await waiting;

        Expect.Equal(SindriErrors.ViewsBehind.Code, result.Error.Code);
        Expect.True(result.Error.Message.Contains('3', StringComparison.Ordinal) && result.Error.Message.Contains("10", StringComparison.Ordinal));
    }

    /// <summary>While stalled behind the target, the wait fails at once (it would never be reached) and names the stall.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task WaitFor_fails_at_once_while_stalled()
    {
        var subject = Create(Clock);
        subject.Publish(Status(cursor: 3, head: 10) with { Stalled = true, StalledAt = 4, StalledType = "service.ran" });

        var waiting = subject.Status.WaitFor(10, Limit, Token);

        Expect.True(waiting.IsCompleted);
        var result = await waiting;
        Expect.Equal(SindriErrors.ViewsBehind.Code, result.Error.Code);
        Expect.True(result.Error.Message.Contains("stalled", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A rebuild request shows as rebuilding at once, before the projector picks it up.</summary>
    [Fact]
    public void RequestRebuild_shows_rebuilding_at_once()
    {
        var subject = Create(Clock);
        subject.Publish(Status(cursor: 7, head: 7));

        subject.Control.RequestRebuild();

        Expect.True(subject.Status.Current.Rebuilding);
    }

    private ViewsStatus Status(long cursor, long head) =>
        new(cursor, head, Clock.GetUtcNow(), Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null);
}
