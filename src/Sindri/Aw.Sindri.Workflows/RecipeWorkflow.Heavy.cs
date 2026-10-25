using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Heavy;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The heavy resource (line spec LN-WF3, §8.4; engine spec §3.4, §5.8): 'waitFor: { resource: heavy }' is granted when a slot of the box's
/// heavy-job lock was taken for this instance ('TakeHeavy', holder 'sindri &lt;address&gt;'), as the resource allocator's 'wait.satisfied' with
/// <see cref="ResourceWaitSource"/>. The slot is held while the instance runs Steps off the desk and while it waits for its children, and
/// given up when it is parked, taken over, at a desk Step, cancelled or ended. After a desk Step, a park or a worker restart, the instance
/// is still in its heavy phase, so the next Step off the desk takes a slot first. The pure question, what to do now, is
/// <see cref="HeavyHold"/>. Gated by the 'ln-wf3-heavy-slot' marker: a history without it grants nothing, as it was recorded.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private const string HeavyActor = "service:heavy";
    private static readonly TimeSpan _heavyHeartbeat = TimeSpan.FromSeconds(10);

    private static readonly ActivityOptions _heavyReleaseOptions = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy { MaximumAttempts = 5 },
        CancellationToken = CancellationToken.None,   // detached: a cancelled workflow still gives its slot back
    };

    private bool _heavy;   // the 'ln-wf3-heavy-slot' marker is in this history
    private bool _heavyPhase;   // a heavy wait was granted: durable, a replay rebuilds it
    private bool _heavyLeased;   // a lease may be held for this instance: set when a take starts, cleared when it is released
    private (int Entry, string State)? _heavyAsked;   // the wait entry (or resume) the allocator already answered
    private string? _heavyEnsured;   // the run token a Step off the desk was already ensured for
    private int _heavyGrants;
    private int _heavyTakes;   // the take number: a release covers every take up to it

    private enum Taking
    {
        Taken,
        Interrupted,
        Waiting,
    }

    /// <summary>One pass of the heavy rule before the loop runs a Step or waits.</summary>
    /// <returns>Null when the loop goes on; the instance after a timer or command that cut a take short.</returns>
    private async Task<WorkflowInstance?> HeavyTurn(WorkflowInstance instance, Interpreting context)
    {
        if (!_heavy)
        {
            return null;
        }

        var move = HeavyHold.Of(instance, context.Recipe, _heavyPhase, _heavyLeased);
        if (move != HeavyMove.Acquire)
        {
            _heavyAsked = null;
        }

        switch (move)
        {
            case HeavyMove.Release:
                await GiveUpHeavy(instance).ConfigureAwait(true);
                return null;
            case HeavyMove.Acquire:
                return await AcquireHeavy(instance, context).ConfigureAwait(true);
            case HeavyMove.Ensure when instance.Frame.Run is { } open && !string.Equals(_heavyEnsured, open.Token.Value, StringComparison.Ordinal):
                return await EnsureHeavy(instance, open, context).ConfigureAwait(true);
            case HeavyMove.None:
            case HeavyMove.Ensure:
            default:
                return null;
        }
    }

    // At a heavy wait: take the slot, then grant the wait. A busy answer, a take that ran out or a failure leaves the wait waiting; its timer parks it.
    private async Task<WorkflowInstance?> AcquireHeavy(WorkflowInstance instance, Interpreting context)
    {
        var entry = (instance.Frame.Entry, instance.Frame.State);
        if (_heavyAsked == entry)
        {
            return null;   // asked already: the grant is queued for the wait, or the wait is waiting for its timer
        }

        var held = _heavyLeased;
        switch (await TakeHeavy(instance).ConfigureAwait(true))
        {
            case Taking.Interrupted:
                return await ApplyMeanwhile(instance, context).ConfigureAwait(true);
            case Taking.Taken:
                _heavyPhase = true;
                await MirrorHeld(instance, held).ConfigureAwait(true);
                _delivered.Enqueue(new WaitSatisfied(HeavyActor, new ResourceWaitSource(ResourceKind.Heavy, ++_heavyGrants), Outcome: null, new Dictionary<FactKey, FactValue>()));
                break;
            case Taking.Waiting:
            default:
                break;
        }

        _heavyAsked = entry;
        return null;
    }

    // Before a Step off the desk in the heavy phase: a slot is held for it (a no-op when it already is, a take after a release or a restart).
    private async Task<WorkflowInstance?> EnsureHeavy(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        var held = _heavyLeased;
        switch (await TakeHeavy(instance).ConfigureAwait(true))
        {
            case Taking.Interrupted:
                return await ApplyMeanwhile(instance, context).ConfigureAwait(true);
            case Taking.Taken:
                _heavyEnsured = open.Token.Value;
                await MirrorHeld(instance, held).ConfigureAwait(true);
                return null;
            case Taking.Waiting:
            default:
                await Workflow.WaitConditionAsync(() => _submitted.Count > 0 || _timers.Fired).ConfigureAwait(true);   // no slot yet: the Step's timer or a command ends the wait
                return await ApplyMeanwhile(instance, context).ConfigureAwait(true);
        }
    }

    /// <summary>The time left on the state's timer (at least a second), or a day when it runs none: how long a take may wait.</summary>
    private TimeSpan HeavyBound()
    {
        if (_timers.FiresAt() is not { } at)
        {
            return TimeSpan.FromDays(1);
        }

        var left = at - Now;
        return left > TimeSpan.FromSeconds(1) ? left : TimeSpan.FromSeconds(1);
    }

    private async Task<Taking> TakeHeavy(WorkflowInstance instance)
    {
        var bound = HeavyBound();
        var take = new HeavyTake(instance.Address.Value, bound, ++_heavyTakes);
        using var cancel = new CancellationTokenSource();
        _heavyLeased = true;   // from here a lease may exist, whatever the answer is
        var running = Workflow.ExecuteActivityAsync(
            (HeavyActivities a) => a.TakeAsync(take),
            new ActivityOptions
            {
                StartToCloseTimeout = bound + TimeSpan.FromMinutes(1),
                ScheduleToCloseTimeout = bound + TimeSpan.FromMinutes(5),
                HeartbeatTimeout = _heavyHeartbeat,
                RetryPolicy = new RetryPolicy { MaximumAttempts = 3 },
                CancellationToken = cancel.Token,
            });
        while (!running.IsCompleted)
        {
            await ExpireHeld(instance, wait: null).ConfigureAwait(true);
            if (_submitted.Count > 0 || _timers.Fired)
            {
                Cancel(cancel);
                await Abandoned(running).ConfigureAwait(true);
                return Taking.Interrupted;
            }

            var seen = _arrivals;
            bool Woken() => running.IsCompleted || _submitted.Count > 0 || _timers.Fired || _arrivals != seen;
            _ = await Workflow.WaitConditionAsync(Woken, NextExpiry() ?? Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        }

        try
        {
            return await running.ConfigureAwait(true) ? Taking.Taken : Taking.Waiting;
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            return Taking.Waiting;   // boundary: a lock directory that can't be used is a slot not yet free; the wait's timer decides
        }
    }

    private static async Task Abandoned(Task<bool> running)
    {
        try
        {
            _ = await running.ConfigureAwait(true);
        }
        catch (Exception e) when (e is OperationCanceledException or FailureException)
        {
            // boundary: the cancelled take's outcome is not wanted; the lease it may have made is given back by address
        }
    }

    private async Task GiveUpHeavy(WorkflowInstance instance)
    {
        var release = new HeavyRelease(instance.Address.Value, _heavyTakes);
        await Workflow.ExecuteActivityAsync((HeavyActivities a) => a.ReleaseAsync(release), _heavyReleaseOptions).ConfigureAwait(true);   // covers a cancelled take still running on the worker
        _heavyLeased = false;
        _heavyEnsured = null;
        await MirrorHeavy(instance, "heavy.released").ConfigureAwait(true);
    }

    private async Task GiveUpHeavyAtEnd(WorkflowInstance ended)
    {
        if (_heavy && _heavyLeased)
        {
            await GiveUpHeavy(ended).ConfigureAwait(true);
        }
    }

    // A cancelled workflow can't mirror a row; it gives the slot back by address in a detached scope.
    private async Task ReleaseHeavyDetached()
    {
        if (!_heavy || !_heavyLeased || _start is not { } start)
        {
            return;
        }

        var release = new HeavyRelease(start.WorkflowId().Value, _heavyTakes);
        try
        {
            await Workflow.ExecuteActivityAsync((HeavyActivities a) => a.ReleaseAsync(release), _heavyReleaseOptions).ConfigureAwait(true);
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            // boundary: the lease goes with the worker if this can't run; a failed release must not change how the instance ended
        }

        _heavyLeased = false;
    }

    // A take that found the lease already held writes no row.
    private Task MirrorHeld(WorkflowInstance instance, bool alreadyHeld) => alreadyHeld ? Task.CompletedTask : MirrorHeavy(instance, "heavy.held");

    // One 'workflow.heavy' row per grant and per release; the activity id keys it, so a replay writes it once.
    private Task MirrorHeavy(WorkflowInstance instance, string trigger) => Mirror(
        new WorkflowEventRecord(_start!.WorkflowId().Value, EventTypes.WorkflowHeavy, _start.Item.Value, instance.State, instance.State, trigger, Outcome: null, instance.Frame.Run?.Token.Value, HeavyActor),   // why: the loop runs only after Begin set _start
        NeedsYou(instance));
}
