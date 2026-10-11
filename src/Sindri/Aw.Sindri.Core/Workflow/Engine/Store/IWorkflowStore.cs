using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Store;

/// <summary>
/// The engine's store port (spec §5.3, W13): instance rows, their events and the recorded decisions. Every change to
/// a row goes through <see cref="Commit"/>. The in-memory fake (P3) and SqliteWorkflowStore (P4) pass the same
/// WorkflowStoreContract.
/// </summary>
public interface IWorkflowStore
{
    /// <summary>The instance <paramref name="instance"/>.</summary>
    /// <param name="instance">Its id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or null when there is none.</returns>
    public ValueTask<WorkflowInstance?> Find(WorkflowInstanceId instance, CancellationToken cancellationToken);

    /// <summary>The instance at <paramref name="address"/>.</summary>
    /// <param name="address">A CLI address.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or null when there is none.</returns>
    public ValueTask<WorkflowInstance?> FindByAddress(InstanceAddress address, CancellationToken cancellationToken);

    /// <summary>The children of <paramref name="parent"/>, oldest first.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Its children's rows.</returns>
    public ValueTask<IReadOnlyList<WorkflowInstance>> Children(WorkflowInstanceId parent, CancellationToken cancellationToken);

    /// <summary>The decision recorded under <paramref name="key"/>.</summary>
    /// <param name="key">A trigger key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recorded decision, or null when the key is unused.</returns>
    public ValueTask<RecordedDecision?> Recorded(TriggerKey key, CancellationToken cancellationToken);

    /// <summary>The events of <paramref name="instance"/>, oldest first, from its workflow.started.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Its events.</returns>
    public ValueTask<IReadOnlyList<WorkflowEvent>> Events(WorkflowInstanceId instance, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="commit"/> in one transaction (spec §5.3). A recorded key returns the recorded decision
    /// and writes nothing; a row whose version moved returns SND-WF-002 and writes nothing.
    /// </summary>
    /// <param name="commit">The commit.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The receipt, or SND-WF-002.</returns>
    public ValueTask<Result<CommitReceipt>> Commit(WorkflowCommit commit, CancellationToken cancellationToken);
}
