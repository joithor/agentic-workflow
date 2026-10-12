using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'workflow.eventHold' (engine spec §5.4, S1-D11): Temporal buffers a delivered event until a wait takes it, and the
/// interpreter drops one held longer than the hold, measured from the event's own time (<see cref="WaitSatisfied.OccurredAt"/>),
/// with one 'workflow.expired' ledger event. A wait wakes when the oldest held event runs out, so the expiry is recorded
/// on time; an event that arrives already past the hold is dropped before any wait can take it.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private TimeSpan _eventHold;
    private int _arrivals;

    /// <summary>
    /// Waits until <paramref name="ready"/> holds, expiring held events as they run out. Returns false when it woke only to
    /// expire or to re-arm for a newly held event (the caller waits again); a held event already past the hold never makes
    /// it ready.
    /// </summary>
    private async Task<bool> WaitHolding(Func<bool> ready, WorkflowInstance instance)
    {
        await ExpireHeld(instance).ConfigureAwait(true);
        var seen = _arrivals;
        bool Woken() => ready() || _arrivals != seen;   // a new held event may run out before the ones already held
        var woke = NextExpiry() is { } left
            ? await Workflow.WaitConditionAsync(Woken, left).ConfigureAwait(true)
            : await Workflow.WaitConditionAsync(Woken, Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        await ExpireHeld(instance).ConfigureAwait(true);   // what ran out, and one that arrived already past the hold
        return woke && ready();
    }

    /// <summary>Drops every held event older than the hold, each with one 'workflow.expired' row; the others stay, in order.</summary>
    private async Task ExpireHeld(WorkflowInstance instance)
    {
        var now = Now;
        List<WaitSatisfied> expired = [];
        for (var left = _delivered.Count; left > 0; left--)
        {
            var held = _delivered.Dequeue();
            if (held.OccurredAt is { } at && now - at >= _eventHold)
            {
                expired.Add(held);
            }
            else
            {
                _delivered.Enqueue(held);
            }
        }

        foreach (var held in expired)
        {
            var record = new WorkflowEventRecord(
                _start!.WorkflowId().Value, // why: the loop runs only after RunAsync set _start
                TypeOf(WorkflowEventType.Expired), _start.Item.Value, instance.State, instance.State, held.Name, held.Outcome?.Value, instance.Frame.Run?.Token.Value);
            await Mirror(record, NeedsYou(instance)).ConfigureAwait(true);
        }
    }

    /// <summary>How long until the oldest held event runs out; null when none is held with a time.</summary>
    private TimeSpan? NextExpiry()
    {
        var now = Now;
        TimeSpan? next = null;
        foreach (var held in _delivered)
        {
            if (held.OccurredAt is { } at && (next is null || at + _eventHold - now < next))
            {
                next = at + _eventHold - now;
            }
        }

        return next is { } span ? TimeSpan.FromTicks(Math.Max(span.Ticks, TimeSpan.TicksPerMillisecond)) : null;
    }
}
