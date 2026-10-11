using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;
using Edges = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>The timeout rules (spec §3.4, §17.28.5) and the loop-cap rules (spec §3.7, A §17.23.13).</summary>
internal sealed partial class GraphRules
{
    /// <summary>
    /// RCP-TIME-001: an active or wait state without a timeout, reported where the state starts. RCP-TIME-002: 'then:
    /// retry' on a child wait, which would start a second child instead of re-arming the same wait.
    /// </summary>
    private static IEnumerable<RecipeFinding> Timeouts(Recipe recipe, RecipeSource source)
    {
        foreach (var (name, state) in recipe.States.Where(s => s.Value.Kind != StateKind.Terminal))
        {
            if (state.Timeout is null)
            {
                var kind = state.Kind == StateKind.Active ? "active" : "wait";
                yield return source.Finding(
                    "RCP-TIME-001",
                    $"states.{name}.timeout",
                    $"{kind} state '{name}' has no timeout",
                    "add timeout: { after, then: park | escalate | retry }");
            }
            else if (state.Timeout.Then == TimeoutAction.Retry && state.WaitFor?.Child is not null)
            {
                yield return source.Finding(
                    "RCP-TIME-002",
                    $"states.{name}.timeout.then",
                    $"state '{name}' retries a child wait when it times out",
                    "use then: park or then: escalate, since a child can't be retried");
            }
        }
    }

    /// <summary>
    /// RCP-LOOP-001: a state names a loop the recipe doesn't declare, or a loop's cap is below 1. RCP-LOOP-002: a
    /// state in a loop isn't on a cycle, so its counter can never pass round 1. RCP-LOOP-003: an empty noProgress.
    /// </summary>
    private static IEnumerable<RecipeFinding> Loops(Recipe recipe, Edges edges, RecipeSource source)
    {
        foreach (var (name, loop) in recipe.Loops)
        {
            if (loop.Cap < 1)
            {
                yield return source.Finding(
                    "RCP-LOOP-001",
                    $"loops.{name}.cap",
                    string.Create(CultureInfo.InvariantCulture, $"loop '{name}' has cap {loop.Cap}"),
                    "set cap to 1 or more");
            }

            if (loop.NoProgress.Count == 0)
            {
                yield return source.Finding(
                    "RCP-LOOP-003",
                    $"loops.{name}.noProgress",
                    $"loop '{name}' has an empty noProgress",
                    "name at least one of findings, emptyDiff and sameFailure, or leave noProgress out to use all three");
            }
        }

        foreach (var (name, state) in recipe.States)
        {
            var finding = LoopMembership(recipe, name, state, edges, source);
            if (finding is not null)
            {
                yield return finding;
            }
        }
    }

    private static RecipeFinding? LoopMembership(Recipe recipe, string name, RecipeState state, Edges edges, RecipeSource source)
    {
        if (state.Loop is not { } loop)
        {
            return null;
        }

        if (!recipe.Loops.ContainsKey(loop))
        {
            return source.Finding(
                "RCP-LOOP-001",
                $"states.{name}.loop",
                $"state '{name}' names loop '{loop}', which isn't declared",
                "declare it under loops, or name a declared loop");
        }

        return Reached([name], edges).Contains(name)
            ? null
            : source.Finding(
                "RCP-LOOP-002",
                $"states.{name}.loop",
                $"state '{name}' is in loop '{loop}' but no transition leads back into it, so the loop never counts a second round",
                "add the transition that leads back into it, or remove loop");
    }
}
