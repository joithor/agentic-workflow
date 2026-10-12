using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's state loop (engine spec rev 8 §5.1, §5.2, §5.4, §5.8; engine P4 unit (a)). Every trigger reaches
/// the instance at a wait and is decided there, one at a time, by P3's pure <see cref="TransitionDecision"/>: a command
/// an Update hands over through <see cref="Submit"/> (units (b), (c)), or a registered event the bridge delivered on the
/// 'event' signal (unit (d)) that fits the wait state's 'waitFor'. An event that fits no wait yet stays buffered until
/// one does (§5.4). An applied or refused trigger writes one ledger event for the instance (§5.6) and sets the search
/// attributes; a rejected one records nothing. Workflow code only: no I/O, clock or random outside 'Workflow.*'.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private readonly Queue<Submission> _submitted = new();
    private readonly Dictionary<int, Decision> _decided = [];
    private int _tickets;

    /// <summary>
    /// The seam the Updates call (units (b), (c)): hands <paramref name="trigger"/> to the loop, which decides it at the
    /// instance's current wait, applies and mirrors it, and returns the decision. Its validator has already run.
    /// </summary>
    /// <param name="trigger">The command's trigger (a report, an answer, a command, a cancel …).</param>
    /// <returns>The decision: applied, refused, absorbed, or rejected (nothing recorded).</returns>
    private async Task<Decision> Submit(Trigger trigger)
    {
        var ticket = ++_tickets;
        _submitted.Enqueue(new Submission(ticket, trigger));
        await Workflow.WaitConditionAsync(() => _decided.ContainsKey(ticket)).ConfigureAwait(true);
        _ = _decided.Remove(ticket, out var decision);
        return decision ?? throw new InvalidOperationException("the loop settled the ticket without a decision");
    }

    /// <summary>
    /// Waits for the next trigger and decides it (§5.4, §5.8): in a wait state, the first delivered event that fits its
    /// 'waitFor' or a submitted command, whichever is first; parked, escalated or taken over, a submitted command. Wait
    /// timeouts are P5's. A child wait is P7's: the child isn't started yet, so only its delivered end or a command moves it.
    /// </summary>
    private async Task<WorkflowInstance> Wait(WorkflowInstance instance, Interpreting context)
    {
        var wait = instance.Suspension is null && context.Recipe.States.TryGetValue(instance.Frame.State, out var state) ? state.WaitFor : null;
        await Workflow.WaitConditionAsync(() => _submitted.Count > 0 || _delivered.Any(e => Fits(e, wait, instance.Frame))).ConfigureAwait(true);
        return _submitted.TryDequeue(out var submission)
            ? await Apply(instance, submission, context).ConfigureAwait(true)
            : await Apply(instance, new Submission(Ticket: 0, Take(wait, instance.Frame)), context).ConfigureAwait(true);
    }

    /// <summary>
    /// Decides one trigger and applies its change (§5.1, §5.6): the instance after it, one ledger event through
    /// 'RecordEvent', the search attributes; the decision goes back to the Update that submitted it.
    /// </summary>
    private async Task<WorkflowInstance> Apply(WorkflowInstance instance, Submission submission, Interpreting context)
    {
        var decision = TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, submission.Trigger, GateAnswers.None, context.Decisions));
        var after = After(instance, decision);
        if (OwnChange(instance, decision) is { } change)
        {
            Enter(after);
            await Mirror(Record(instance, change, submission.Trigger), NeedsYou(after)).ConfigureAwait(true);
        }

        if (submission.Ticket != 0)
        {
            _decided[submission.Ticket] = decision;
        }

        return after;
    }

    /// <summary>The first delivered event that fits the wait, taken out of the buffer; the others stay, in order.</summary>
    private WaitSatisfied Take(WaitFor? wait, Frame frame)
    {
        WaitSatisfied? taken = null;
        for (var left = _delivered.Count; left > 0; left--)
        {
            var delivered = _delivered.Dequeue();
            if (taken is null && Fits(delivered, wait, frame))
            {
                taken = delivered;
            }
            else
            {
                _delivered.Enqueue(delivered);
            }
        }

        return taken ?? throw new InvalidOperationException("no delivered event fits the wait the loop woke for");
    }

    /// <summary>Whether <paramref name="delivered"/> is what the wait waits for: its event kind, its resource, or its child's end.</summary>
    private static bool Fits(WaitSatisfied delivered, WaitFor? wait, Frame frame) => (wait, delivered.Source) switch
    {
        (null, _) => false,
        (_, EventWaitSource e) => wait.Event == e.Kind,
        (_, ResourceWaitSource r) => wait.Resource == r.Resource,
        (_, ChildWaitSource c) => wait.Child is not null && frame.Child == c.Child,
        _ => false,
    };

    /// <summary>The decision's change to this instance (an applied or refused trigger); none for a rejected or absorbed one.</summary>
    private static InstanceChange? OwnChange(WorkflowInstance instance, Decision decision) => decision switch
    {
        Applied applied => applied.Changes.FirstOrDefault(c => c.After.Id == instance.Id),
        Refused refused => refused.Changes.FirstOrDefault(c => c.After.Id == instance.Id),
        _ => null,
    };

    /// <summary>The ledger row for <paramref name="change"/>: its event type, the states, the trigger, its outcome and the run it reported on.</summary>
    private WorkflowEventRecord Record(WorkflowInstance before, InstanceChange change, Trigger trigger) => new(
        _start!.WorkflowId().Value, // why: the loop runs only after RunAsync set _start
        TypeOf(change.Event.Type),
        _start.Item.Value,
        before.State,
        change.After.State,
        trigger.Name,
        trigger switch
        {
            StepCompleted completed => completed.Outcome.Value,
            WaitSatisfied satisfied => satisfied.Outcome?.Value,
            _ => null,
        },
        before.Frame.Run?.Token.Value);

    private static string TypeOf(WorkflowEventType type) => type switch
    {
        WorkflowEventType.Started => EventTypes.WorkflowStarted,
        WorkflowEventType.Refused => "workflow.refused",
        WorkflowEventType.Shifted => "workflow.shifted",
        WorkflowEventType.Notified => "workflow.notified",
        WorkflowEventType.Expired => "workflow.expired",
        WorkflowEventType.EffectRefused => "workflow.effect-refused",
        WorkflowEventType.Transition or _ => EventTypes.WorkflowTransition,
    };

    /// <summary>
    /// Sets the search attributes (spec §5.1) as they stand: 'Item', 'Recipe', 'State', 'NeedsYou', 'Tenant' and 'Ring'
    /// from the input and the instance, and 'Repo' when 'fact:item' named one.
    /// </summary>
    private void Upsert(string state, bool needsYou)
    {
        var start = _start!; // why: Mirror runs only after RunAsync set _start
        List<SearchAttributeUpdate> updates =
        [
            SindriTemporal.ItemKey.ValueSet(start.Item.Value),
            SindriTemporal.RecipeKey.ValueSet(start.Recipe.Id.Value),
            SindriTemporal.StateKey.ValueSet(state),
            SindriTemporal.NeedsYouKey.ValueSet(needsYou),
            SindriTemporal.TenantKey.ValueSet(start.Tenant),
            SindriTemporal.RingKey.ValueSet(start.Ring.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ];
        if (_repo is { } repo)
        {
            updates.Add(SindriTemporal.RepoKey.ValueSet(repo));
        }

        Workflow.UpsertTypedSearchAttributes([.. updates]);
    }

    /// <summary>The repo 'fact:item' names ('ReadItemRepo', a local activity on start); none when it can't be read.</summary>
    private static async Task<string?> RepoOf(FactValue item)
    {
        try
        {
            return await Workflow.ExecuteLocalActivityAsync(
                (ItemFactActivities a) => a.ReadRepoAsync(item),
                new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10), RetryPolicy = new RetryPolicy { MaximumAttempts = 3 } }).ConfigureAwait(true);
        }
        catch (ActivityFailureException)
        {
            return null; // boundary: a fact:item that can't be read leaves 'Repo' unset (spec §5.1); the start goes on
        }
    }
}
