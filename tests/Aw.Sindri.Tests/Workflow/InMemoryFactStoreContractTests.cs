using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryFactStoreContractTests : FactStoreContract
{
    protected override IFactStore Create() => new InMemoryFactStore();
}
