using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Store;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory workflow store, for unit tests; passes <see cref="WorkflowStoreContract"/>.</summary>
public sealed class InMemoryWorkflowStore : IWorkflowStore
{
    /// <inheritdoc/>
    public ValueTask<WorkflowInstance?> Find(WorkflowInstanceId instance, CancellationToken cancellationToken) =>
        throw new NotSupportedException("P3 unit (e): InMemoryWorkflowStore.Find");

    /// <inheritdoc/>
    public ValueTask<WorkflowInstance?> FindByAddress(InstanceAddress address, CancellationToken cancellationToken) =>
        throw new NotSupportedException("P3 unit (e): InMemoryWorkflowStore.FindByAddress");

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WorkflowInstance>> Children(WorkflowInstanceId parent, CancellationToken cancellationToken) =>
        throw new NotSupportedException("P3 unit (e): InMemoryWorkflowStore.Children");

    /// <inheritdoc/>
    public ValueTask<RecordedDecision?> Recorded(TriggerKey key, CancellationToken cancellationToken) =>
        throw new NotSupportedException("P3 unit (e): InMemoryWorkflowStore.Recorded");

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WorkflowEvent>> Events(WorkflowInstanceId instance, CancellationToken cancellationToken) =>
        throw new NotSupportedException("P3 unit (e): InMemoryWorkflowStore.Events");

    /// <inheritdoc/>
    public ValueTask<Result<CommitReceipt>> Commit(WorkflowCommit commit, CancellationToken cancellationToken) =>
        throw new NotSupportedException("P3 unit (e): InMemoryWorkflowStore.Commit");
}
