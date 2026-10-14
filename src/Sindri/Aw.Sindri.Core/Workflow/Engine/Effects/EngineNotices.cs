using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// The engine's own notices (engine spec §5.7 "origin decides the exemption", §6.3; engine P6 unit (c)): a park, an
/// escalation, takeoverIdle or a desk task each emits one 'effect.notify.desk' with origin 'engine', exempt from the
/// recheck, signalling nothing. Its identity is '{instance id}:{entry}:n{version}' (<see cref="EffectIdentity.ForNotice"/>),
/// so two parks in one entry get distinct identities.
/// </summary>
public static class EngineNotices
{
    /// <summary>The notices a change from <paramref name="before"/> to <paramref name="after"/> sends, with their class.</summary>
    /// <param name="before">The instance before the change (null at start).</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="version">The instance's version after the change ('v&lt;n&gt;', the mirrored event count).</param>
    /// <param name="deskTaskNotified">Whether the instance already sent a desk-task notice (only the first interrupts).</param>
    /// <returns>Each notice's command and class.</returns>
    public static IReadOnlyList<(EffectCommand Command, NoticeClass Class)> For(WorkflowInstance? before, WorkflowInstance after, int version, bool deskTaskNotified)
    {
        ArgumentNullException.ThrowIfNull(after);
        _ = (before, version, deskTaskNotified);
        throw new NotSupportedException("engine P6 unit (c): EngineNotices.For (parks, escalations, desk tasks, takeoverIdle)");
    }

    /// <summary>The notices a change of type <paramref name="change"/> sends: a 'workflow.notified' change is takeoverIdle's (P5).</summary>
    /// <param name="before">The instance before the change (null at start).</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="version">The instance's version after the change.</param>
    /// <param name="deskTaskNotified">Whether the instance already sent a desk-task notice.</param>
    /// <param name="change">The type of the change's ledger event.</param>
    /// <returns>Each notice's command and class.</returns>
    public static IReadOnlyList<(EffectCommand Command, NoticeClass Class)> For(WorkflowInstance? before, WorkflowInstance after, int version, bool deskTaskNotified, WorkflowEventType change)
    {
        ArgumentNullException.ThrowIfNull(after);
        _ = (before, version, deskTaskNotified, change);
        throw new NotSupportedException("engine P6 unit (c): EngineNotices.For");
    }
}
