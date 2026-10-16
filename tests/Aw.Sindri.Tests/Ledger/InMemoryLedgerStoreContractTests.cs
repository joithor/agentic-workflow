using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Ledger;

[Lane(TestLane.Unit)]
public sealed class InMemoryLedgerStoreContractTests : LedgerStoreContract
{
    protected override ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe)
    {
        var store = new InMemorySindriStore(clock, probe);
        return ValueTask.FromResult(new SindriStores(store, store));
    }

    protected override ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe, ILedgerSignal signal)
    {
        var store = new InMemorySindriStore(clock, probe, signal);
        return ValueTask.FromResult(new SindriStores(store, store));
    }
}
