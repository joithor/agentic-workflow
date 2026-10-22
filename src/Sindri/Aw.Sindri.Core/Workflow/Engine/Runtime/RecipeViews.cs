using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Builds the API's <see cref="RecipeView"/> (API spec §3.8, E-22) from a recipe: a pure function of the recipe and its stamp.</summary>
public static class RecipeViews
{
    /// <summary>
    /// The view of <paramref name="recipe"/>: its inputs (with the declared subject kind, or the placeholder a pre-P4c row gets, and whether each is required), its outputs, the urgency,
    /// and the states in path order with the loops that carry a cap, all from the same <see cref="StatePathOrder"/> the instance cells use.
    /// </summary>
    /// <param name="recipe">The recipe as registered.</param>
    /// <param name="stamp">Its id, version and hash.</param>
    /// <param name="displayName">The name shown instead of the id; null takes the recipe's own displayName (null when it has none, and the view leaves it out).</param>
    /// <returns>The view.</returns>
    public static RecipeView Of(Recipe recipe, RecipeVersion stamp, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(stamp);
        var path = StatePathOrder.Of(recipe);
        return new RecipeView(
            stamp.Id,
            stamp.Hash,
            stamp.Version,
            recipe.Name,
            [.. recipe.Inputs.Select(static i => new RecipeInputView(i.Key.Value, i.Label, FactTypes.Parse(SubjectKinds.TypeOf(i)), i.Kind ?? SubjectKinds.NoKind, Required: i.Generate is null, i.Generate))],
            [.. recipe.Outputs.Select(static o => new RecipeOutputView(o.Key.Value, o.Label, Declared(o.Type), o.From))],
            Urgencies.IsKnown(recipe.Urgency) ? Urgencies.Parse(recipe.Urgency) : RecipeUrgency.Work,
            path.OrderedBy == "transition-list" ? PathOrdering.TransitionList : PathOrdering.Path,
            [.. path.States.Select(s => State(recipe, s))],
            [.. path.Loops.Where(l => l.Name is { } name && recipe.Loops.ContainsKey(name)).Select(l => new RecipeLoopView(l.Name, l.From, l.To, recipe.Loops[l.Name!].Cap))],   // why: the Where kept only the named ones
            displayName ?? recipe.DisplayName,
            Labelled(recipe));
    }

    private static RecipeStateView State(Recipe recipe, StatePlace state) =>
        recipe.States.TryGetValue(state.State, out var declared)
            ? new RecipeStateView(state.State, state.Order, state.BranchOf, declared.Why, declared.DisplayName)
            : new RecipeStateView(state.State, state.Order, state.BranchOf);

    /// <summary>The transitions that carry a label, in file order; null (left out of the JSON) when none does.</summary>
    private static List<RecipeTransitionLabelView>? Labelled(Recipe recipe)
    {
        var labelled = recipe.Transitions.Where(static t => t.Label is not null).Select(static t => new RecipeTransitionLabelView(t.From, t.To, t.On, t.Label!)).ToList();   // why: the Where kept only the labelled ones
        return labelled.Count == 0 ? null : labelled;
    }

    private static FactType Declared(string? type) => FactTypes.Parse(type ?? Recipes.FactTypes.Default);
}
