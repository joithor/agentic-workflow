using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The context engine's heavy-job lock over Sindri's lock port: a busy lock is SND-CONTEXT-040 naming the holder (CLI exit 75, HTTP 503).</summary>
public sealed class SindriHeavyJobLockTests
{
    private sealed class Slots(Result<HeavySlotTaken> answer) : IHeavySlots
    {
        public string? Holder { get; private set; }

        public TimeSpan? Timeout { get; private set; }

        public ValueTask<Result<HeavySlotTaken>> Take(string holder, TimeSpan timeout, IProgress<string>? waiting, CancellationToken cancellationToken)
        {
            Holder = holder;
            Timeout = timeout;
            waiting?.Report("waiting for heavy-job lock (slot 1/2 held by 77)");
            return ValueTask.FromResult(answer);
        }
    }

    private sealed class Lease : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_free_slot_is_a_lease_taken_for_the_map_build_and_waiting_lines_reach_the_progress()
    {
        var lease = new Lease();
        var slots = new Slots(new HeavySlotTaken(lease, string.Empty));
        var told = new List<string>();

        var taken = await new SindriHeavyJobLock(slots, TimeSpan.FromSeconds(30)).Take(new Progress<string>(told.Add), TestContext.Current.CancellationToken);
        await taken.Value.DisposeAsync();

        Assert.Equal(("sindri context-map-build", TimeSpan.FromSeconds(30)), (slots.Holder, slots.Timeout));
        Assert.True(lease.Disposed);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Contains("waiting for heavy-job lock (slot 1/2 held by 77)", told, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lock_that_stays_busy_is_040_naming_the_holder_and_the_wait()
    {
        var slots = new Slots(new HeavySlotTaken(Lease: null, "slot 1/2 held by 77 (heavy), slot 2/2 held by 78 (heavy)"));

        var taken = await new SindriHeavyJobLock(slots, TimeSpan.FromSeconds(60)).Take(waiting: null, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.HeavyLockBusy.Code, taken.Error.Code);
        Assert.Contains("slot 1/2 held by 77", taken.Error.Message, StringComparison.Ordinal);
        Assert.Contains("60 s", taken.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lock_directory_that_cant_be_used_is_040_too_so_a_build_never_runs_without_its_slot()
    {
        var slots = new Slots(SindriErrors.ServiceFailed.With("mkdir /x failed (errno 13)"));

        var taken = await new SindriHeavyJobLock(slots, TimeSpan.Zero).Take(waiting: null, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.HeavyLockBusy.Code, taken.Error.Code);
        Assert.Contains("errno 13", taken.Error.Message, StringComparison.Ordinal);
    }
}
