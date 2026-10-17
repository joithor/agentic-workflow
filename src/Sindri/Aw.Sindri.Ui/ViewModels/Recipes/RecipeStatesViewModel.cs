using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>
/// The recipe's states in the order the API serves them (screens spec §4.3). A branch folds under the state it branches off. A loop between
/// two neighbouring states is drawn between them with its cap; any other loop (into the same state, or back over others) is a note on the
/// state it leaves. Each output sits under the state it comes from. Nothing is worked out: no terminal, desk or child mark, which the
/// recipe does not serve.
/// </summary>
internal sealed class RecipeStatesViewModel
{
    internal RecipeStatesViewModel(RecipeView recipe)
    {
        var main = recipe.States.Where(s => s.BranchOf is null).ToList();
        var joined = main.Select((cell, i) => i + 1 < main.Count ? recipe.Loops.Where(l => Joins(l, cell.State, main[i + 1].State)).ToList() : []).ToList();
        var context = new Context(recipe, [.. joined.SelectMany(l => l)]);
        Nodes =
        [
            .. main.Select((cell, i) => Node(
                context, cell, i + 1 < main.Count ? main[i + 1] : null, joined[i],
                [.. recipe.States.Where(b => string.Equals(b.BranchOf, cell.State, StringComparison.Ordinal)).Select(b => Node(context, b, next: null, [], []))])),
        ];
        OrderNote = recipe.OrderedBy == PathOrdering.TransitionList ? Labels.OrderedByTransitions : string.Empty;
    }

    /// <summary>The states in served order, branches folded under their source.</summary>
    public IReadOnlyList<RecipeStateNodeViewModel> Nodes { get; }

    /// <summary>The header note when the recipe has no path; empty otherwise.</summary>
    public string OrderNote { get; }

    /// <summary>True when there is an order note.</summary>
    public bool HasOrderNote => OrderNote.Length > 0;

    /// <summary>The band's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Heading => Labels.StatesHeading;
#pragma warning restore CA1822, MA0041

    // Two neighbours are joined by a loop that goes from either to the other; a loop into the same state joins none.
    private static bool Joins(RecipeLoopView loop, string a, string b) =>
        (string.Equals(loop.From, a, StringComparison.Ordinal) && string.Equals(loop.To, b, StringComparison.Ordinal))
        || (string.Equals(loop.From, b, StringComparison.Ordinal) && string.Equals(loop.To, a, StringComparison.Ordinal));

    private static RecipeStateNodeViewModel Node(Context context, RecipeStateView cell, RecipeStateView? next, List<RecipeLoopView> between, IReadOnlyList<RecipeStateNodeViewModel> branches)
    {
        var link = string.Join(' ', between.Select(l => Labels.LoopLink(l.Name, l.Cap)));   // only a served loop joins two states: the recipe serves no other transition
        var spoken = new List<string>();
        if (next is not null)
        {
            spoken.AddRange(between.Select(l => $"{Labels.LoopsWith(next.State)}, {Labels.LoopCapWords(l.Name, l.Cap)}"));
        }

        if (cell.BranchOf is { Length: > 0 } source)
        {
            spoken.Add(Labels.BranchOf(source));
        }

        return new RecipeStateNodeViewModel(cell.State, link, Notes(context, cell.State), Outputs(context.Recipe, cell.State), branches, string.Join(", ", spoken));
    }

    private static List<string> Notes(Context context, string state) =>
        [.. context.Recipe.Loops
            .Where(l => string.Equals(l.From, state, StringComparison.Ordinal) && !context.Drawn.Contains(l))
            .Select(l => string.Equals(l.To, state, StringComparison.Ordinal) ? Labels.LoopNoteSelf(l.Name, l.Cap) : Labels.LoopNote(l.Name, l.To, l.Cap))];

    private static List<string> Outputs(RecipeView recipe, string state) =>
        [.. recipe.Outputs.Where(o => string.Equals(o.From, state, StringComparison.Ordinal)).Select(o => Labels.OutputLine(o.Label ?? o.Key, Labels.FactTypeWord(o.Type)))];

    // The recipe and the loops already drawn between neighbours (so they are not notes too).
    private sealed record Context(RecipeView Recipe, HashSet<RecipeLoopView> Drawn);
}
