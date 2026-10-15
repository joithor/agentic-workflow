using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IDelay contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class DelayContract
{
    private static readonly TimeSpan _timerSlack = TimeSpan.FromMilliseconds(30);

    /// <summary>The adapter under test and a reading of the clock it waits on.</summary>
    /// <param name="now">Reads the clock the delay's time passes on.</param>
    /// <returns>The adapter.</returns>
    protected abstract IDelay Create(out Func<DateTimeOffset> now);

    /// <summary>
    /// After a wait, about that much time has passed on the clock. A timer may fire a few milliseconds before the wall
    /// clock shows the full delay (the two clocks tick apart), so the check allows that much slack and the wait is long
    /// enough that a delay which returned at once still fails.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_wait_lets_the_time_pass()
    {
        var delay = Create(out var now);
        var before = now();

        await delay.Wait(TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Expect.That(now() - before >= TimeSpan.FromMilliseconds(100) - _timerSlack, "the clock moved by about the wait");
    }

    /// <summary>A cancelled wait is cancelled.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_cancelled_wait_throws()
    {
        var delay = Create(out _);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        var cancelled = false;
        try
        {
            await delay.Wait(TimeSpan.FromSeconds(30), cancel.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Expect.That(cancelled, "a cancelled token ends the wait");
    }
}
