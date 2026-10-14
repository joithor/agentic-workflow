using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The engine triggers Phase 5 adds (engine spec §3.4, §3.8, §5.2, §5.5; UI spec E-6): a state or retry timer that
/// fired, the takeover's idle reminder, and an 'open-request'. Engine P5 contract unit: the decisions are stubs, unit
/// (a) for the state timers and unit (b) for 'takeoverIdle' and 'open-request'.
/// </summary>
public static partial class TransitionDecision
{
    /// <summary>
    /// A state or retry timer fired in the entry it was armed in (spec §3.4, §5.5): 'then: park' parks with 'timeout',
    /// 'then: escalate' escalates (kind timeout), 'then: retry' re-arms once in the same entry and re-emits the entering
    /// transition's effects (new command ids, the same effect identity), and a retry timer that fires parks with
    /// 'timeout'. In desk mode every 'then' parks (W20). A timer of an entry that has ended, or of a suspended instance,
    /// is absorbed. Unit (a).
    /// </summary>
    private static Decision Expired(Deciding d, WorkflowInstance instance, DeadlineFired fired) =>
        throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture, $"engine P5 unit (a): the {fired.Kind} timer of entry {fired.Entry} on {instance.Address.Value} ({d.Key.Value})"));

    /// <summary>
    /// The takeover's idle reminder fired (spec §5.5, §6.3): while still taken over, one 'workflow.notified' and one
    /// engine-origin 'notify.desk' ("hand back {item}?"), and the instance stays as it is; it never hands back. After a
    /// handback it is absorbed. Unit (b).
    /// </summary>
    private static Decision Idle(Deciding d, WorkflowInstance instance, DeadlineFired fired) =>
        throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture, $"engine P5 unit (b): takeoverIdle of entry {fired.Entry} on {instance.Address.Value} ({d.Key.Value})"));

    /// <summary>
    /// An 'open-request' (UI spec E-6; spec §5.2): a takeover with actor 'service:engine' wherever a takeover is allowed,
    /// exempt from the approval check; absorbed (a no-op) when the instance is already taken over; SND-WF-003 when it
    /// has ended, which the launcher reports as the launch's failure. Unit (b).
    /// </summary>
    private static Decision OpenRequest(Deciding d, WorkflowInstance instance, OpenRequested open) =>
        throw new NotSupportedException($"engine P5 unit (b): the open-request for {open.Launch} on {instance.Address.Value} ({d.Key.Value})");
}
