using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class SlotPoolTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_freed_slot_goes_to_the_lowest_job_then_the_lowest_step()
    {
        var pool = new SlotPool(1);
        var held = await pool.Acquire(0, 0, TestContext.Current.CancellationToken);
        var order = new List<(int Job, int Step)>();
        var waiting = new[] { (3, 0), (2, 1), (2, 0), (1, 5) }.Select(async w =>
        {
            using var slot = await pool.Acquire(w.Item1, w.Item2, TestContext.Current.CancellationToken);
            lock (order)
            {
                order.Add(w);
            }
        }).ToList();

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        held.Dispose();
        await Task.WhenAll(waiting);

        Assert.Equal([(1, 5), (2, 0), (2, 1), (3, 0)], order);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_more_than_the_limit_are_held_at_once()
    {
        var pool = new SlotPool(2);
        var first = await pool.Acquire(1, 0, TestContext.Current.CancellationToken);
        var second = await pool.Acquire(2, 0, TestContext.Current.CancellationToken);

        var third = pool.Acquire(3, 0, TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        Assert.False(third.IsCompleted);

        first.Dispose();
        first.Dispose();
        (await third).Dispose();
        second.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_waiter_leaves_the_line_without_taking_a_slot()
    {
        var pool = new SlotPool(1);
        var held = await pool.Acquire(1, 0, TestContext.Current.CancellationToken);
        using var cancel = new CancellationTokenSource();
        var waiter = pool.Acquire(2, 0, cancel.Token).AsTask();
        await cancel.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);

        held.Dispose();

        using var next = await pool.Acquire(3, 0, TestContext.Current.CancellationToken);
    }
}
