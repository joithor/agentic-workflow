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
        var heavy = CreateLock(busyHolder: null, capacity: 1);

        var slot = Expect.Ok(await heavy.Take(waiting: null, None), test);
        var whileHeld = await heavy.Take(waiting: null, None);
        await slot.DisposeAsync();
        var again = Expect.Ok(await heavy.Take(waiting: null, None), test);
        await again.DisposeAsync();

        Expect.That(whileHeld.IsError && whileHeld.Error.Code == "SND-CONTEXT-040", test, "a second take while the only slot was held wasn't SND-CONTEXT-040");
    }

    /// <summary>A lock with two slots lets two holders in and refuses the third until one lets go.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_lock_with_two_slots_holds_two_holders_and_refuses_a_third()
    {
        const string test = nameof(A_lock_with_two_slots_holds_two_holders_and_refuses_a_third);
        var heavy = CreateLock(busyHolder: null, capacity: 2);

        var first = Expect.Ok(await heavy.Take(waiting: null, None), test);
        var second = Expect.Ok(await heavy.Take(waiting: null, None), test);
        var third = await heavy.Take(waiting: null, None);
        await first.DisposeAsync();
        var fourth = Expect.Ok(await heavy.Take(waiting: null, None), test);
        await second.DisposeAsync();
        await fourth.DisposeAsync();

        Expect.That(third.IsError && third.Error.Code == "SND-CONTEXT-040", test, "the third holder of two slots wasn't refused");
    }

    /// <summary>A lock that stays busy answers SND-CONTEXT-040 and says who holds it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_busy_lock_is_SND_CONTEXT_040_naming_the_holder()
    {
        const string test = nameof(A_busy_lock_is_SND_CONTEXT_040_naming_the_holder);
        var heavy = CreateLock(busyHolder: "pid 4242", capacity: 2);
        var taken = await heavy.Take(waiting: null, None);

        Expect.That(taken.IsError && taken.Error.Code == "SND-CONTEXT-040", test, "a busy lock wasn't SND-CONTEXT-040");
        Expect.That(taken.Error.Message.Contains("pid 4242", StringComparison.Ordinal), test, "the holder wasn't named");
    }

    /// <summary>A lock whose slots another holder has, or a free one.</summary>
    /// <param name="busyHolder">Who holds every slot, or null when a slot is free.</param>
    /// <param name="capacity">How many slots the lock has.</param>
    /// <returns>The lock.</returns>
    protected abstract IHeavyJobLock CreateLock(string? busyHolder, int capacity);
}
