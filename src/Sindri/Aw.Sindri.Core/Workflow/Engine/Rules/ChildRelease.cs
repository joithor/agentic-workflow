using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Ends the parent's suspension that mirrors a child (spec §5.8: any later change from the child pops that mirror
/// first), and drops a pending park that mirrors it. A park that waited behind the mirror takes effect.
/// </summary>
internal static class ChildRelease
{
    /// <summary>See <see cref="ChildPropagation.Release"/>; the same instance when nothing mirrors <paramref name="child"/>.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="child">The child the change is from.</param>
    /// <returns>The parent with nothing mirroring that child.</returns>
    public static WorkflowInstance Release(WorkflowInstance parent, WorkflowInstanceId child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var pending = parent.PendingPark?.Mirrors == child ? null : parent.PendingPark;
        if (parent.Suspension?.Mirrors == child)
        {
            return parent with { Suspension = pending, PendingPark = null };
        }

        return ReferenceEquals(pending, parent.PendingPark) ? parent : parent with { PendingPark = pending };
    }
}
