using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>When an instance can be run again: it ended without succeeding (failed, didn't work out or cancelled) and is a root.</summary>
public static class Reruns
{
    /// <summary>The catalog action that starts the same recipe again.</summary>
    public static ActionId Action { get; } = ActionId.From("RerunInstance");

    /// <summary>The rerun action <paramref name="view"/> offers: one on a root instance that ended 'failed', 'unresolved' or 'cancelled'; none on a finished one ('resolved', 'alreadySatisfied'), a live one or a child.</summary>
    /// <param name="view">The instance view.</param>
    /// <returns>The action, or nothing.</returns>
    public static IReadOnlyList<ActionId> Of(InstanceView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.ParentId is null && view.Outcome?.Value is "failed" or "unresolved" or "cancelled" ? [Action] : [];
    }
}
