using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
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
    /// <param name="catalog">The build's block descriptors, which say who does each state and which facts its step reads and writes; null leaves those fields out (a state that waits on a child is still a sub-workflow).</param>
    /// <returns>The view.</returns>
    public static RecipeView Of(Recipe recipe, RecipeVersion stamp, string? displayName = null, BlockCatalog? catalog = null)
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
            [.. path.States.Select(s => State(recipe, s, catalog))],
            [.. path.Loops.Where(l => l.Name is { } name && recipe.Loops.ContainsKey(name)).Select(l => new RecipeLoopView(l.Name, l.From, l.To, recipe.Loops[l.Name!].Cap))],   // why: the Where kept only the named ones
            displayName ?? recipe.DisplayName,
            Labelled(recipe),
            recipe.Why,
            [.. recipe.Transitions.Select(static t => new RecipeTransitionView(t.From, t.On, t.To, t.When?.Outcome?.Value, t.Label))]);
    }

    private static RecipeStateView State(Recipe recipe, StatePlace state, BlockCatalog? catalog)
    {
        if (!recipe.States.TryGetValue(state.State, out var declared))
        {
            return new RecipeStateView(state.State, state.Order, state.BranchOf);
        }

        var descriptor = declared.Step is { } step ? catalog?.Find(step.Use) : null;
        return new RecipeStateView(
            state.State, state.Order, state.BranchOf, declared.Why, declared.DisplayName,
            KindOf(declared, descriptor),
            OnOutcome(recipe, declared, descriptor),
            Facts(descriptor is not null && declared.Step is { } use ? StepInputs.Of(descriptor, use.With, recipe) ?? descriptor.Inputs : null, recipe),
            Facts(descriptor?.Outputs.Select(static o => o.Fact), recipe));
    }

    /// <summary>Who does the state: a child wait is a sub-workflow; else the step's runtime (agent, block) or the desk (you, or only you when the step is human-only); null for a state with no step the catalog knows.</summary>
    private static RecipeStateKind? KindOf(RecipeState state, BlockDescriptor? step) =>
        state.WaitFor?.Child is not null ? RecipeStateKind.SubWorkflow
        : step switch
        {
            null => null,
            { Runtime: BlockRuntime.Agent } => RecipeStateKind.Agent,
            { Runtime: BlockRuntime.Block } => RecipeStateKind.Automatic,
            { HumanOnly: true } => RecipeStateKind.OnlyYou,
            { Runtime: BlockRuntime.Desk } or { DeskCapable: true } => RecipeStateKind.You,
            _ => null,
        };

    /// <summary>The recipe's words for each outcome, in the step's outcome order (a key the step doesn't report follows, in file order); null when the state writes none.</summary>
    private static List<RecipeOutcomeText>? OnOutcome(Recipe recipe, RecipeState state, BlockDescriptor? step)
    {
        if (state.OnOutcome is not { Count: > 0 } texts)
        {
            return null;
        }

        var order = OutcomeOrder(recipe, state, step);
        var listed = order.Concat(texts.Keys).Distinct(StringComparer.Ordinal)
            .Where(o => texts.TryGetValue(o, out var text) && !string.IsNullOrWhiteSpace(text))
            .Select(o => new RecipeOutcomeText(o, texts[o]))
            .ToList();
        return listed.Count == 0 ? null : listed;
    }

    private static IEnumerable<string> OutcomeOrder(Recipe recipe, RecipeState state, BlockDescriptor? step) =>
        step is not null && state.Step is { } use ? (StepOutcomes.Of(step, use, recipe) ?? step.Outcomes).Select(static o => o.Value) : [];

    /// <summary>The facts as the fact rows key them, each with the recipe's label when it declares one; null when there are none.</summary>
    private static List<RecipeFactView>? Facts(IEnumerable<FactKey>? keys, Recipe recipe)
    {
        var facts = (keys ?? []).Select(k => new RecipeFactView(k.Value, LabelOf(recipe, k))).ToList();
        return facts.Count == 0 ? null : facts;
    }

    private static string? LabelOf(Recipe recipe, FactKey key) =>
        recipe.Inputs.FirstOrDefault(i => i.Key == key)?.Label ?? recipe.Outputs.FirstOrDefault(o => o.Key == key)?.Label;

    /// <summary>The transitions that carry a label, in file order; null (left out of the JSON) when none does.</summary>
    private static List<RecipeTransitionLabelView>? Labelled(Recipe recipe)
    {
        var labelled = recipe.Transitions.Where(static t => t.Label is not null).Select(static t => new RecipeTransitionLabelView(t.From, t.To, t.On, t.Label!)).ToList();   // why: the Where kept only the labelled ones
        return labelled.Count == 0 ? null : labelled;
    }

    private static FactType Declared(string? type) => FactTypes.Parse(type ?? Recipes.FactTypes.Default);
}
