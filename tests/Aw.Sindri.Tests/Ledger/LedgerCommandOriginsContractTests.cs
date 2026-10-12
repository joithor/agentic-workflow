using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Ledger;

[Lane(TestLane.Integration)]
public sealed class LedgerCommandOriginsContractTests : CommandOriginsContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override ValueTask<(SindriStores Stores, ICommandOrigins Origins)> Create(TimeProvider clock, IProcessProbe probe)
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), clock);
        var stores = new SindriStores(new SqliteLease(database, probe, clock), new SqliteLedger(database));
        return ValueTask.FromResult<(SindriStores, ICommandOrigins)>((stores, new LedgerCommandOrigins(database)));
    }
}
