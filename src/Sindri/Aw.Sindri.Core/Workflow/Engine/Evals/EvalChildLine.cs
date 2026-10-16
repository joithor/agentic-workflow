using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>What a 'wait.satisfied' eval line may give for a child's end (DT spec §8.4): its declared outputs under the child's own keys, and none on 'cancelled'.</summary>
internal static class EvalChildLine
{
    /// <summary>Why the line can't stand for the child's end, or null when it can (or the wait has no resolvable child, which RCP-CHILD-001 reports).</summary>
    public static string? Refusal(WaitFor wait, EvalTrigger line, IEnumerable<FactKey> given, IChildRecipeResolver children)
    {
        var keys = given.ToList();
        if (wait.Child is not { } child || keys.Count == 0)
        {
            return null;
        }

        if (line.Outcome is { } outcome && outcome.Value == "cancelled")
        {
            return "a cancelled child carries no outputs (fix: remove outputs from this line)";
        }

        if (children.Resolve(child.Recipe) is not { } found)
        {
            return null;
        }

        var declared = ChildOutputs.Declared(found.Recipe);
        var undeclared = keys.Where(k => !declared.Contains(k)).ToList();
        if (undeclared.Count == 0)
        {
            return null;
        }

        var id = child.Recipe.Value;
        return $"wait.satisfied from child '{id}' gives {string.Join(", ", undeclared.Select(static k => k.Value))}, which {id} doesn't declare as outputs "
            + (declared.Count == 0 ? $"(fix: remove outputs; {id} declares none)" : $"(fix: give only its outputs: {string.Join(", ", declared.Select(static k => k.Value))})");
    }
}
