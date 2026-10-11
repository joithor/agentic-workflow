using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Store;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

[Lane(TestLane.Unit)]
public sealed class InMemoryWorkflowStoreContractTests : WorkflowStoreContract
{
    protected override ValueTask<IWorkflowStore> Create(TimeProvider clock) => ValueTask.FromResult<IWorkflowStore>(new InMemoryWorkflowStore());
}
