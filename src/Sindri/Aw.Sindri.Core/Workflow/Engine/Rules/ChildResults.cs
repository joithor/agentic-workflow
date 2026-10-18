using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>What a child's end hands its parent (DT spec §8.4, engine P7 plan D4): the child's declared outputs.</summary>
public static class ChildResults
{
    /// <summary>
    /// The child's declared 'outputs' that are among its facts when it ended, under the child's own keys. Whatever outcome
    /// ended it carries them, 'resolved', 'unresolved', 'failed' or any other; 'cancelled' carries none. A fact the child
    /// holds but doesn't declare never reaches the parent. The parent's 'produces' picks from these (RCP-WIRE-003 has
    /// already checked at validate time that every path to the outcome produces them).
    /// </summary>
    /// <param name="child">The child's recipe.</param>
    /// <param name="ended">The child as it ended.</param>
    /// <returns>The outputs the parent's 'wait.satisfied' carries.</returns>
    public static IReadOnlyDictionary<FactKey, FactValue> Outputs(Recipe child, WorkflowInstance ended)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(ended);
        if (ended.Outcome is { } outcome && string.Equals(outcome.Value, "cancelled", StringComparison.Ordinal))
        {
            return new Dictionary<FactKey, FactValue>();
        }

        var declared = child.Outputs.Select(static o => o.Key).ToHashSet();
        return ended.Facts.Where(f => declared.Contains(f.Key)).ToDictionary(f => f.Key, f => f.Value);
    }
}
