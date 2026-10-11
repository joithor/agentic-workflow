using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Store;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The in-memory workflow store, for unit tests; passes <see cref="WorkflowStoreContract"/>. A Commit is all or
/// nothing (spec W13, §5.3): a recorded key returns its decision and writes nothing; otherwise every row must still
/// be at the version its write expects (0: not there yet), or the Commit is SND-WF-002 and writes nothing; then the
/// rows, their events, the decision under its key, the outbox commands and the consumed nonce are written together.
/// </summary>
public sealed class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<WorkflowInstanceId, WorkflowInstance> _rows = [];
    private readonly List<WorkflowInstanceId> _created = [];
    private readonly Dictionary<WorkflowInstanceId, List<WorkflowEvent>> _events = [];
    private readonly Dictionary<TriggerKey, RecordedDecision> _decisions = [];
    private readonly List<EffectCommand> _outbox = [];
    private readonly HashSet<CommandNonce> _nonces = [];

    /// <summary>The outbox commands written so far, oldest first.</summary>
    public IReadOnlyList<EffectCommand> Outbox { get { lock (_gate) { return [.. _outbox]; } } }

    /// <summary>The nonces Commits consumed so far.</summary>
    public IReadOnlySet<CommandNonce> ConsumedNonces { get { lock (_gate) { return _nonces.ToHashSet(); } } }

    /// <inheritdoc/>
    public ValueTask<WorkflowInstance?> Find(WorkflowInstanceId instance, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_rows.GetValueOrDefault(instance));
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowInstance?> FindByAddress(InstanceAddress address, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_rows.Values.FirstOrDefault(row => row.Address == address));
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WorkflowInstance>> Children(WorkflowInstanceId parent, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<WorkflowInstance> children = [.. _created.Select(id => _rows[id]).Where(row => row.Parent == parent)];
            return ValueTask.FromResult(children);
        }
    }

    /// <inheritdoc/>
    public ValueTask<RecordedDecision?> Recorded(TriggerKey key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_decisions.GetValueOrDefault(key));
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WorkflowEvent>> Events(WorkflowInstanceId instance, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<WorkflowEvent> events = _events.TryGetValue(instance, out var recorded) ? [.. recorded] : [];
            return ValueTask.FromResult(events);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<CommitReceipt>> Commit(WorkflowCommit commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commit);
        lock (_gate)
        {
            if (_decisions.TryGetValue(commit.Decision.Key, out var recorded))
            {
                return ValueTask.FromResult<Result<CommitReceipt>>(new CommitReceipt(recorded, AlreadyApplied: true));
            }

            if (commit.Writes.FirstOrDefault(Moved) is { } moved)
            {
                return ValueTask.FromResult<Result<CommitReceipt>>(WorkflowErrors.InstanceChanged.With(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{moved.After.Address.Value} is no longer at version {moved.FromVersion} (fix: read it again and retry)")));
            }

            foreach (var write in commit.Writes)
            {
                Write(write);
            }

            _decisions.Add(commit.Decision.Key, commit.Decision);
            _outbox.AddRange(commit.Outbox);
            if (commit.Nonce is { } nonce)
            {
                _ = _nonces.Add(nonce);
            }

            return ValueTask.FromResult<Result<CommitReceipt>>(new CommitReceipt(commit.Decision, AlreadyApplied: false));
        }
    }

    private bool Moved(InstanceWrite write) => _rows.TryGetValue(write.After.Id, out var row) ? row.Version != write.FromVersion : write.FromVersion != 0;

    private void Write(InstanceWrite write)
    {
        var id = write.After.Id;
        if (!_rows.ContainsKey(id))
        {
            _created.Add(id);
            _events[id] = [];
        }

        _rows[id] = write.After;
        _events[id].Add(write.Event);
    }
}
