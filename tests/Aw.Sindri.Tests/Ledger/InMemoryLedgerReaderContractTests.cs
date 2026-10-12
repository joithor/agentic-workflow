using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Ledger;

[Lane(TestLane.Unit)]
public sealed class InMemoryLedgerReaderContractTests : LedgerReaderContract
{
    private InMemorySindriStore? _store;

    protected override ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe)
    {
        _store = new InMemorySindriStore(clock, probe);
        return ValueTask.FromResult(new SindriStores(_store, _store));
    }

    protected override ILedgerReader Reader() => _store ?? throw new InvalidOperationException("Create first");

    protected override bool LedgerExists() => false;
}
