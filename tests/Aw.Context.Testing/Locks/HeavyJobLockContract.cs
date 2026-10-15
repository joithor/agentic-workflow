using Aw.Context.Core.Locks;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Locks;

/// <summary>The heavy-job lock's contract (S-4): a slot is held until disposed, and a lock that stays busy is SND-CONTEXT-040 naming the holder.</summary>
public abstract class HeavyJobLockContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>A slot is free when taken and free again once disposed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_slot_is_held_until_it_is_disposed()
    {
        const string test = nameof(A_slot_is_held_until_it_is_disposed);
        var heavy = CreateLock(busyHolder: null);

        var slot = Expect.Ok(await heavy.Take(waiting: null, None), test);
        await slot.DisposeAsync();
        var again = Expect.Ok(await heavy.Take(waiting: null, None), test);
        await again.DisposeAsync();
    }

    /// <summary>A lock that stays busy answers SND-CONTEXT-040 and says who holds it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_busy_lock_is_SND_CONTEXT_040_naming_the_holder()
    {
        const string test = nameof(A_busy_lock_is_SND_CONTEXT_040_naming_the_holder);
        var heavy = CreateLock(busyHolder: "pid 4242");
        var taken = await heavy.Take(waiting: null, None);

        Expect.That(taken.IsError && taken.Error.Code == "SND-CONTEXT-040", test, "a busy lock wasn't SND-CONTEXT-040");
        Expect.That(taken.Error.Message.Contains("pid 4242", StringComparison.Ordinal), test, "the holder wasn't named");
    }

    /// <summary>A lock whose slots another holder has, or a free one.</summary>
    /// <param name="busyHolder">Who holds every slot, or null when a slot is free.</param>
    /// <returns>The lock.</returns>
    protected abstract IHeavyJobLock CreateLock(string? busyHolder);
}
