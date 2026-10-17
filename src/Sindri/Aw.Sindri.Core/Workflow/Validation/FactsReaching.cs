using System.Globalization;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Available = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<Aw.Sindri.Core.Workflow.Ids.FactKey>>;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>Which facts a recipe has recorded when each state is reached; RCP-WIRE-001 and RCP-WIRE-003 read it, and so does a child's outputs.</summary>
internal static class FactsReaching
{
    /// <summary>
    /// The facts recorded on every path into each state the start reaches (a must-analysis to its greatest fixpoint):
    /// the initial state has the recipe's inputs; any other state has what every transition into it from a reached
    /// state carries (that state's facts plus its 'produces'). A state the start doesn't reach has no entry.
    /// </summary>
    public static Available Of(Recipe recipe)
    {
        var available = new Available(StringComparer.Ordinal);
        if (!recipe.States.ContainsKey(recipe.Initial))
        {
            return available;
        }

        available[recipe.Initial] = [.. recipe.Inputs.Select(i => i.Key)];
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var transition in recipe.Transitions)
            {
                foreach (var from in transition.From)
                {
                    changed |= Carry(recipe, available, from, transition);
                }
            }
        }

        return available;
    }

    private static bool Carry(Recipe recipe, Available available, string from, RecipeTransition transition)
    {
        if (transition.To == recipe.Initial || !recipe.States.ContainsKey(transition.To) || !available.TryGetValue(from, out var facts))
        {
            return false;
        }

        HashSet<FactKey> carried = [.. facts, .. transition.Produces.Select(p => p.Fact)];
        if (!available.TryGetValue(transition.To, out var current))
        {
            available[transition.To] = carried;
            return true;
        }

        var before = current.Count;
        current.IntersectWith(carried);
        return current.Count != before;
    }

    /// <summary>Why <paramref name="fact"/> isn't there in <paramref name="state"/>: the start, or the first path into it that doesn't carry it.</summary>
    public static string Witness(Recipe recipe, Available available, string state, FactKey fact)
    {
        if (state == recipe.Initial)
        {
            return "the start doesn't supply it";
        }

        var path = recipe.Transitions
            .Select((t, i) => (Transition: t, Index: i))
            .Where(p => p.Transition.To == state && !p.Transition.Produces.Any(q => q.Fact == fact))
            .SelectMany(p => p.Transition.From.Select(from => (p.Index, From: from)))
            .FirstOrDefault(p => available.TryGetValue(p.From, out var facts) && !facts.Contains(fact));
        return path.From is null ? "not every path into it produces it" : $"the path through transitions[{path.Index.ToString(CultureInfo.InvariantCulture)}] from '{path.From}' doesn't produce it";
    }
}
