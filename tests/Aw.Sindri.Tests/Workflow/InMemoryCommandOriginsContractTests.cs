using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryCommandOriginsContractTests : CommandOriginsContract
{
    protected override ValueTask<(SindriStores Stores, ICommandOrigins Origins)> Create(TimeProvider clock, IProcessProbe probe)
    {
        var store = new InMemorySindriStore(clock, probe);
        return ValueTask.FromResult<(SindriStores, ICommandOrigins)>((new SindriStores(store, store), new InMemoryCommandOrigins(store)));
    }
}
