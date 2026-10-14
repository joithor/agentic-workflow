using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The takeover Updates of <see cref="RecipeWorkflow"/> (engine spec §3.8, §5.2, §5.5, §5.8; UI spec E-6, E-19; engine
/// P5 unit (b)): 'takeover' (class pause; a session may take over) suspends the instance and pauses its state timer,
/// keeping what was left on the suspension; 'handback' (class desk) pops back to whatever the takeover covered and
/// re-arms that timer with what was left; 'open-request' is the takeover the engine sends itself (caller
/// 'service:engine') when a launch is requested for the item, keyed 'wf:{instance}:open:{launch}', a no-op when already
/// taken over. All three are exempt from the approval check. Each validator runs the pure decision on the instance as
/// it stands and rejects what it rejects (SND-WF-003), recording nothing; the accepted Update hands its trigger to the
/// loop through <see cref="Submit"/>. The contract unit leaves them as stubs.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>'workflow takeover' (class pause, spec §5.5): suspends the instance and pauses its timer. Unit (b).</summary>
    /// <param name="takeover">The takeover.</param>
    /// <returns>The instance after it.</returns>
    [WorkflowUpdate("takeover")]
    public Task<CommandResult> TakeoverAsync(TakeoverRequested takeover)
    {
        var remaining = _timers.Pause(_instance?.Frame.Entry ?? 0);   // the seam: what was left goes on the takeover's suspension
        throw Unbuilt($"P5 unit (b): the takeover Update ({takeover?.Actor}, {remaining?.Left.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s left)");
    }

    /// <summary>Rejects a takeover where none is allowed: an ended instance, or one already taken over (SND-WF-003). Unit (b).</summary>
    /// <param name="takeover">The takeover.</param>
    [WorkflowUpdateValidator(nameof(TakeoverAsync))]
    public void ValidateTakeover(TakeoverRequested takeover) => throw Unbuilt($"P5 unit (b): the takeover validator ({takeover?.Actor})");

    /// <summary>'workflow handback' (class desk, spec §5.5): ends the takeover and re-arms the timer with what remained. Unit (b).</summary>
    /// <param name="handback">The handback.</param>
    /// <returns>The instance after it.</returns>
    [WorkflowUpdate("handback")]
    public Task<CommandResult> HandbackAsync(HandbackRequested handback)
    {
        _timers.Resume(_instance?.Suspension?.Remaining, _instance?.Frame.Entry ?? 0);   // the seam: re-arm what the takeover left
        throw Unbuilt($"P5 unit (b): the handback Update ({handback?.Actor})");
    }

    /// <summary>Rejects a handback of an instance that isn't taken over (SND-WF-003). Unit (b).</summary>
    /// <param name="handback">The handback.</param>
    [WorkflowUpdateValidator(nameof(HandbackAsync))]
    public void ValidateHandback(HandbackRequested handback) => throw Unbuilt($"P5 unit (b): the handback validator ({handback?.Actor})");

    /// <summary>
    /// An 'open-request' (UI spec E-6, spec §5.2): the engine's own takeover before the launcher opens a session for the
    /// item. Update id 'wf:{instance}:open:{launch}' (<see cref="InstanceIds.OpenRequestUpdate"/>), no nonce, actor
    /// 'service:engine', exempt from the approval check; a no-op when already taken over. The launcher waits for this
    /// result. Unit (b).
    /// </summary>
    /// <param name="request">The open-request.</param>
    /// <returns>The instance after it: taken over, or as it was.</returns>
    [WorkflowUpdate("open-request")]
    public Task<CommandResult> OpenRequestAsync(OpenRequested request) => throw Unbuilt($"P5 unit (b): the open-request Update ({request?.Launch})");

    /// <summary>Rejects an open-request where no takeover is allowed: an ended instance (SND-WF-003). Unit (b).</summary>
    /// <param name="request">The open-request.</param>
    [WorkflowUpdateValidator(nameof(OpenRequestAsync))]
    public void ValidateOpenRequest(OpenRequested request) => throw Unbuilt($"P5 unit (b): the open-request validator ({request?.Launch})");
}
