using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's effects (engine spec rev 8 §5.7, §3.4; engine P6 unit (a)). After a change's ledger event the loop
/// hands its effects to <see cref="Emit"/>, which starts <see cref="Dispatch"/> as a workflow task it doesn't await, so
/// the instance enters its next state while a recipe effect waits out the dispatch delay. An effect identity runs once
/// per instance: one already dispatched, or still on its way, is skipped, so a 'retry' re-emission (<see cref="Reemit"/>)
/// writes nothing twice. A cancel within the delay aborts the effect. The instance returns only once every effect it
/// emitted is dispatched, refused or aborted.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private const string Dispatcher = "system:dispatcher";
    private readonly List<Task> _effects = [];
    private readonly HashSet<string> _effectsOpen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _effectsSent = new(StringComparer.Ordinal);
    private CancellationTokenSource? _effectsCancel;
    private TimeSpan _dispatchDelay;
    private bool _deskTaskNotified;

    /// <summary>
    /// Runs <paramref name="effects"/> of the change that left the instance as <paramref name="after"/>: each through
    /// 'RecordEffect' (pending); a recipe effect then waits 'workflow.dispatchDelay' (cancelled if the instance is) and
    /// passes 'RecheckEffect' (unit (b)), or is refused ('RecordEffect' refused, then an 'OutboxRefused' trigger); then
    /// 'RunEffect'. An engine notice skips the delay and the recheck: a park interrupts at once.
    /// </summary>
    /// <param name="after">The instance after the change.</param>
    /// <param name="effects">The effects to run.</param>
    /// <returns>Done once every effect is dispatched, refused or aborted.</returns>
    internal Task Dispatch(WorkflowInstance after, IReadOnlyList<EffectCommand> effects)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(effects);
        List<Task> running = [];
        foreach (var effect in effects)
        {
            if (!_effectsSent.Contains(effect.Identity.Text) && _effectsOpen.Add(effect.Identity.Text))
            {
                running.Add(DispatchOne(after, effect));
            }
        }

        return Workflow.WhenAllAsync(running);
    }

    /// <summary>
    /// A 'retry' re-emits the effects of the transition that entered the instance's state, with their identities (§3.4,
    /// §5.5): P5's retry arm calls this. Each one already dispatched is skipped.
    /// </summary>
    /// <param name="instance">The instance whose timeout retries.</param>
    internal void Reemit(WorkflowInstance instance) => Emit(before: null, instance, EffectRules.Reemitted(instance, _context!.Recipe)); // why: the loop runs only after Begin set _context

    /// <summary>
    /// Starts the effects of a change just mirrored: the recipe's effects for this instance and the engine's own notices.
    /// A change that cancels the instance aborts every effect still in its delay.
    /// </summary>
    private void Emit(WorkflowInstance? before, WorkflowInstance after, IReadOnlyList<EffectCommand> effects)
    {
        List<EffectCommand> all = [.. effects.Where(e => e.Identity.Instance == after.Id), .. Notices(before, after)];
        if (all.Count > 0)
        {
            _effects.Add(Dispatch(after, all));
        }

        if (after.Ended && string.Equals(after.Frame.State, EngineStates.Cancelled, StringComparison.Ordinal))
        {
            Effects().Cancel();
        }
    }

    /// <summary>The effects a decision runs (§5.7): an applied or refused trigger's.</summary>
    private static IReadOnlyList<EffectCommand> EffectsOf(Decision? decision) => decision switch
    {
        Applied applied => applied.Effects,
        Refused refused => refused.Effects,
        _ => [],
    };

    /// <summary>The effect ids a change's ledger event lists (§5.6); null when it runs none.</summary>
    private static List<string>? Ids(IReadOnlyList<EffectIdentity>? effects) => effects is { Count: > 0 } ? [.. effects.Select(static e => e.Text)] : null;

    /// <summary>The effect ids of a decision's own change (§5.6); null for the desk timeout, which runs none.</summary>
    private static List<string>? EffectIds(WorkflowInstance before, Decision? decision) =>
        decision is null ? null : Ids(OwnChange(before, decision)?.Event.Effects);

    /// <summary>
    /// The engine's own notices for a change (§5.7 origin 'engine'): parks, escalations, takeoverIdle and desk tasks.
    /// </summary>
    private IReadOnlyList<EffectCommand> Notices(WorkflowInstance? before, WorkflowInstance after)
    {
        IReadOnlyList<(EffectCommand Command, NoticeClass Class)> notices;
        try
        {
            notices = EngineNotices.For(before, after, _mirrored, _deskTaskNotified);
        }
        catch (NotSupportedException)
        {
            return [];   // boundary: engine P6 unit (c) builds EngineNotices.For; until it lands the instance sends no engine notice
        }

        _deskTaskNotified |= notices.Count > 0 && after.Frame.Run is { Runtime: StepRuntime.Desk };
        return [.. notices.Select(static n => n.Command)];
    }

    /// <summary>Waits until every effect the instance emitted has settled (the run's end), surfacing a failed one.</summary>
    private async Task Settle()
    {
        while (_effects.Count > 0)
        {
            Task[] settling = [.. _effects];
            _effects.Clear();
            await Workflow.WhenAllAsync(settling).ConfigureAwait(true);
        }
    }

    private async Task DispatchOne(WorkflowInstance after, EffectCommand effect)
    {
        var row = new EffectRow(effect.Identity.Text, Workflow.NewGuid().ToString("N"), effect.Effect, effect.Origin, EffectState.Pending, after.Address);
        try
        {
            _ = await Record(row).ConfigureAwait(true);
            if (effect.Origin == CommandOrigin.Recipe && !await Cleared(row, effect).ConfigureAwait(true))
            {
                return;
            }

            var run = new EffectRun(after.Address.Value, after.Item.Value, after.Attempt, effect, row.Command, Text: null, after.Facts);
            var options = (ActivityOptions)EffectActivities.RunOptions.Clone();
            options.TaskQueue = Workflow.Info.TaskQueue;   // the instance's queue, from settings (never a constant)
            _ = await Workflow.ExecuteActivityAsync((EffectActivities a) => a.RunAsync(run), options).ConfigureAwait(true);
            _ = _effectsSent.Add(row.Identity);
        }
        finally
        {
            _ = _effectsOpen.Remove(row.Identity);
        }
    }

    /// <summary>
    /// A recipe effect's undo window and recheck (§5.7): false when a cancel aborted it within the delay or the recheck
    /// refused it, each with its 'outbox.refused' row.
    /// </summary>
    private async Task<bool> Cleared(EffectRow row, EffectCommand effect)
    {
        if (!await Delayed().ConfigureAwait(true))
        {
            _ = await Record(row with { State = EffectState.Refused, Reason = "cancelled: the instance was cancelled within the dispatch delay" }).ConfigureAwait(true);
            return false;
        }

        var recheck = new EffectRecheck(_recipe!.Version, effect.Effect, effect.Origin); // why: effects run only after Begin set _recipe
        var checkedNow = await Workflow.ExecuteLocalActivityAsync((EffectActivities a) => a.RecheckAsync(recheck), EffectActivities.LocalOptions).ConfigureAwait(true);
        if (checkedNow.Code is not { } code)
        {
            return true;
        }

        var refused = await Record(row with { State = EffectState.Refused, Code = code }).ConfigureAwait(true);
        await Refuse(new OutboxRefused(Dispatcher, new RefusedEffect(effect.Effect, effect.Identity, effect.With), code, refused.Sequence)).ConfigureAwait(true);
        return false;
    }

    /// <summary>Waits 'workflow.dispatchDelay'; false when the instance was cancelled first.</summary>
    private async Task<bool> Delayed()
    {
        var cancel = Effects();
        if (cancel.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            await Workflow.DelayAsync(_dispatchDelay, cancel.Token).ConfigureAwait(true);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Hands a recheck refusal to the loop (unit (b) decides it), unless the instance has ended meanwhile.</summary>
    private async Task Refuse(OutboxRefused refused)
    {
        var ticket = ++_tickets;
        _submitted.Enqueue(new Submission(ticket, refused));
        await Workflow.WaitConditionAsync(() => _decided.ContainsKey(ticket) || _instance is { Ended: true }).ConfigureAwait(true);
        _ = _decided.Remove(ticket);
    }

    private Task<Core.Ledger.LedgerAppend> Record(EffectRow row) =>
        Workflow.ExecuteLocalActivityAsync((EffectActivities a) => a.RecordAsync(new EffectRecord(_start!.Item.Value, row)), EffectActivities.LocalOptions); // why: effects run only after Begin set _start

    /// <summary>The source that aborts effects still in their delay: the instance's cancel, or the workflow's.</summary>
    private CancellationTokenSource Effects() => _effectsCancel ??= CancellationTokenSource.CreateLinkedTokenSource(Workflow.CancellationToken);
}
