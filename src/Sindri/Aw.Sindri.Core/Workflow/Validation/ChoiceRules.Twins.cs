using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-CHOICE-005 (every route has a desk twin) and RCP-CHOICE-006 (a route lists its branches first).</summary>
internal sealed partial class ChoiceRules
{
    /// <summary>The state a desk twin state names in its state parameter, or null when it names none or isn't a twin.</summary>
    private static string? TwinOf(RecipeState state, BlockCatalog catalog) =>
        state.Step is { } step && catalog.Find(step.Use) is { OutcomesFrom: { } from } && ParameterLists.IsTwin(from)
            ? ParameterLists.NamedState(from, step.With)
            : null;

    /// <summary>The state parameter of the desk twin Step, from its descriptor.</summary>
    private static string TwinParameter(BlockCatalog catalog) =>
        catalog.Find(BlockId.From(TwinId)) is { OutcomesFrom: { } from } ? ParameterLists.StateParameter(from) : "from";

    /// <summary>The twin states whose state parameter is a { fact } or { profile } value: they twin no route that validation can name.</summary>
    private static IEnumerable<RecipeFinding> Unresolved(Recipe recipe, BlockCatalog catalog, RecipeSource source)
    {
        foreach (var (name, state) in recipe.States)
        {
            if (state.Step is { } step && catalog.Find(step.Use) is { OutcomesFrom: { } from } && ParameterLists.IsTwin(from)
                && step.With.TryGetValue(ParameterLists.StateParameter(from), out var named) && Computed(named) is { } computed)
            {
                var parameter = ParameterLists.StateParameter(from);
                yield return source.Finding(
                    "RCP-CHOICE-005", $"states.{name}.step.with.{parameter}", $"{parameter} of state '{name}' is a {computed} value",
                    $"name the route state it twins as a literal: {parameter}: <state>");
            }
        }
    }

    private static IEnumerable<RecipeFinding> Twins(Recipe recipe, List<Route> routes, BlockCatalog catalog, RecipeSource source)
    {
        var unresolved = Unresolved(recipe, catalog, source).ToList();
        if (unresolved.Count > 0)
        {
            // why: the route each such twin belongs to can't be told, so the uncertain and step.failed checks below would only echo this finding
            foreach (var finding in unresolved)
            {
                yield return finding;
            }

            yield break;
        }

        foreach (var (name, state) in recipe.States)
        {
            if (TwinOf(state, catalog) is { } from && !routes.Exists(r => string.Equals(r.State, from, StringComparison.Ordinal)))
            {
                var parameter = TwinParameter(catalog);
                yield return source.Finding(
                    "RCP-CHOICE-005", $"states.{name}.step.with.{parameter}", $"state '{name}' runs '{TwinId}' from '{from}', which isn't a '{RouteId}' state",
                    $"set {parameter}: to the route state it twins");
            }
        }

        foreach (var route in routes)
        {
            var twin = recipe.States.FirstOrDefault(s => string.Equals(TwinOf(s.Value, catalog), route.State, StringComparison.Ordinal)).Key;
            foreach (var finding in Uncertain(recipe, route, catalog, source).Concat(Failed(recipe, route, twin, source)))
            {
                yield return finding;
            }
        }
    }

    private static IEnumerable<RecipeFinding> Uncertain(Recipe recipe, Route route, BlockCatalog catalog, RecipeSource source)
    {
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var t = recipe.Transitions[i];
            if (!IsUncertain(t, route) || (recipe.States.TryGetValue(t.To, out var target) && string.Equals(TwinOf(target, catalog), route.State, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return source.Finding(
                "RCP-CHOICE-005", $"transitions[{Inv(i)}]", $"state '{route.State}' routes uncertain to '{t.To}', which isn't its desk twin",
                $"route it to a state with step: {{ use: {TwinId}, with: {{ {TwinParameter(catalog)}: {route.State} }} }}");
        }
    }

    private static IEnumerable<RecipeFinding> Failed(Recipe recipe, Route route, string? twin, RecipeSource source)
    {
        if (recipe.Transitions.Any(t => IsFailed(t, route.State) && string.Equals(t.To, twin, StringComparison.Ordinal)))
        {
            yield break;
        }

        yield return source.Finding(
            "RCP-CHOICE-005", $"states.{route.State}", $"state '{route.State}' has no step.failed transition to its desk twin",
            $"add {{ from: {route.State}, to: {(string.IsNullOrEmpty(twin) ? "<its desk twin>" : twin)}, on: step.failed }}");
    }

    /// <summary>Whether <paramref name="t"/> takes the route on an outcome its Step adds itself (the descriptor's Outcomes: 'uncertain'), as opposed to one of its choices.</summary>
    private static bool IsUncertain(RecipeTransition t, Route route) =>
        t.From.Contains(route.State, StringComparer.Ordinal) && t.On == "step.completed" && t.When?.Outcome is { } outcome && route.Descriptor.Outcomes.Contains(outcome);

    private static bool IsFailed(RecipeTransition t, string state) => t.From.Contains(state, StringComparer.Ordinal) && t.On == "step.failed";

    private static IEnumerable<RecipeFinding> Order(Recipe recipe, Route route, RecipeSource source)
    {
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var t = recipe.Transitions[i];
            var tail = IsUncertain(t, route) ? "uncertain" : IsFailed(t, route.State) ? "step.failed" : null;
            if (tail is null)
            {
                continue;
            }

            for (var j = i + 1; j < recipe.Transitions.Count; j++)
            {
                var later = recipe.Transitions[j];
                if (later.From.Contains(route.State, StringComparer.Ordinal) && later.On == "step.completed" && !IsUncertain(later, route))
                {
                    yield return source.Finding(
                        "RCP-CHOICE-006", $"transitions[{Inv(i)}]", $"transitions[{Inv(i)}] routes '{route.State}' on {tail} before its branch transitions[{Inv(j)}]",
                        "list the branch transitions first and the uncertain and step.failed ones last");
                    break;
                }
            }
        }
    }
}
