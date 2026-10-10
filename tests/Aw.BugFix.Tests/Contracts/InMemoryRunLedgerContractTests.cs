using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryRunLedgerContractTests : RunLedgerContract
{
    private readonly InMemoryRunLedger _ledger = new();

    protected override IRunLedger Ledger => _ledger;

    protected override string Directory => "/state/x";

    protected override void Edit(string path, RunRecord record) => _ledger.Tamper(path, record);
}
