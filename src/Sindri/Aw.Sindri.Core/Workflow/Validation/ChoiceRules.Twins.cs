using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-CHOICE-005 (every route has a desk twin) and RCP-CHOICE-006 (a route lists its branches first).</summary>
internal sealed partial class ChoiceRules
{
    private static readonly OutcomeName _uncertain = OutcomeName.From("uncertain");

    /// <summary>The state a desk twin state names in 'from', or null when it names none or isn't a twin.</summary>
    private static string? TwinOf(RecipeState state, BlockCatalog catalog) =>
        state.Step is { } step && catalog.Find(step.Use) is { OutcomesFrom: { } from } && ParameterLists.IsTwin(from)
        && step.With.TryGetValue("from", out var named) && named.Literal is { ValueKind: System.Text.Json.JsonValueKind.String } text
            ? text.GetString()
            : null;

    private static IEnumerable<RecipeFinding> Twins(Recipe recipe, List<Route> routes, BlockCatalog catalog, RecipeSource source)
    {
        foreach (var (name, state) in recipe.States)
        {
            if (TwinOf(state, catalog) is { } from && !routes.Exists(r => string.Equals(r.State, from, StringComparison.Ordinal)))
            {
                yield return source.Finding(
                    "RCP-CHOICE-005", $"states.{name}.step.with.from", $"state '{name}' runs '{TwinId}' from '{from}', which isn't a '{RouteId}' state",
                    "set from: to the route state it twins");
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
            if (!IsUncertain(t, route.State) || (recipe.States.TryGetValue(t.To, out var target) && string.Equals(TwinOf(target, catalog), route.State, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return source.Finding(
                "RCP-CHOICE-005", $"transitions[{Inv(i)}]", $"state '{route.State}' routes uncertain to '{t.To}', which isn't its desk twin",
                $"route it to a state with step: {{ use: {TwinId}, with: {{ from: {route.State} }} }}");
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

    private static bool IsUncertain(RecipeTransition t, string state) =>
        t.From.Contains(state, StringComparer.Ordinal) && t.On == "step.completed" && t.When?.Outcome == _uncertain;

    private static bool IsFailed(RecipeTransition t, string state) => t.From.Contains(state, StringComparer.Ordinal) && t.On == "step.failed";

    private static IEnumerable<RecipeFinding> Order(Recipe recipe, Route route, RecipeSource source)
    {
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var t = recipe.Transitions[i];
            var tail = IsUncertain(t, route.State) ? "uncertain" : IsFailed(t, route.State) ? "step.failed" : null;
            if (tail is null)
            {
                continue;
            }

            for (var j = i + 1; j < recipe.Transitions.Count; j++)
            {
                var later = recipe.Transitions[j];
                if (later.From.Contains(route.State, StringComparer.Ordinal) && later.On == "step.completed" && !IsUncertain(later, route.State))
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
