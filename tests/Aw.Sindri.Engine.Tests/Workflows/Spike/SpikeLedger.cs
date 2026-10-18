using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>A ledger with a held lease and the counting 'RecordEvent' over it.</summary>
/// <param name="Ledger">The ledger.</param>
/// <param name="Recorder">The activity the worker registers.</param>
internal sealed record SpikeLedger(ILedgerStore Ledger, CountingRecorder Recorder)
{
    /// <summary>Takes the lease on <paramref name="stores"/> and builds the recorder.</summary>
    /// <param name="stores">The lease and ledger.</param>
    /// <param name="clock">The stores' clock.</param>
    /// <param name="probe">The process probe.</param>
    /// <param name="cancellationToken">Cancels the acquire.</param>
    /// <returns>The ledger.</returns>
    public static async Task<SpikeLedger> Open(SindriStores stores, FakeTimeProvider clock, FakeProcessProbe probe, CancellationToken cancellationToken)
    {
        var held = new HeldLease();
        held.Hold((await stores.Lease.Acquire(probe.Self(), cancellationToken)).Value);
        var activities = new RecordEventActivities(stores.Ledger, held, EventsFor(clock));
        return new SpikeLedger(stores.Ledger, new CountingRecorder(activities));
    }

    /// <summary>An in-memory ledger for tests that don't read it.</summary>
    /// <param name="cancellationToken">Cancels the acquire.</param>
    /// <returns>The ledger.</returns>
    public static Task<SpikeLedger> InMemory(CancellationToken cancellationToken)
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var probe = new FakeProcessProbe();
        var store = new InMemorySindriStore(clock, probe);
        return Open(new SindriStores(store, store), clock, probe, cancellationToken);
    }

    /// <summary>The recorded events of <paramref name="type"/>.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The events, oldest first.</returns>
    public async Task<IReadOnlyList<LedgerEvent>> Read(string type, CancellationToken cancellationToken) =>
        [.. (await Ledger.ReadAfter(0, 10_000, cancellationToken)).Value.Where(e => e.Envelope.Type == type)];

    /// <summary>The control plane's event factory over <paramref name="clock"/>, with the process as the caller.</summary>
    private static EventFactory EventsFor(TimeProvider clock)
    {
        var ids = new SequentialIdGenerator();
        return new EventFactory(EventScope.ControlPlane, clock, ids, SliceHarness.Json, new AsyncLocalCallerContextAccessor(ids));
    }
}
