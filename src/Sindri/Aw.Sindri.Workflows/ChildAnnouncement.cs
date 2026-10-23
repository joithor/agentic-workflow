using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What a child tells its parent about a change in its suspension (spec §5.8).</summary>
internal static class ChildAnnouncement
{
    private const string Inbox = "service:workflow.inbox";

    /// <summary>
    /// The change as the parent receives it: the child's reason and question, and its own park and escalation kind, so the parent's mirror names the
    /// child's real command. A deeper child's kinds, already mirrored here, pass through unchanged.
    /// </summary>
    /// <param name="after">The child after the change.</param>
    /// <param name="kind">What changed.</param>
    /// <param name="mirrored">The count of mirrors, which keys the change.</param>
    /// <returns>The change.</returns>
    public static ChildChanged Of(WorkflowInstance after, ChildChange kind, int mirrored)
    {
        var open = after.Suspension;
        return new ChildChanged(Inbox, after.Id, kind, open?.Reason, open?.Question, TriggerKey.For(after.Id, string.Create(CultureInfo.InvariantCulture, $"child-{mirrored}")),
            open?.ParkKind ?? open?.MirroredParkKind, open?.MirroredEscalation ?? open?.Escalation);
    }
}
