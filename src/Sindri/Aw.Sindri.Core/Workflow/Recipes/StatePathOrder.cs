namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// The path order of a recipe's states (spec §3.10). A pure function of the transition graph, never hashed: a walk from
/// 'initial' that follows transitions in list order. The first transition to a state not yet _seen continues the main
/// path; a later one starts a branch, and every state only that branch reaches is folded under the state it leaves.
/// A transition back to a state still on the walk is a loop edge. Terminal states are numbered after the _others.
/// </summary>
public static class StatePathOrder
{
    /// <summary>The number the first main-path state gets (spec §3.10 numbers from 1). The one place to change if the numbering becomes 0-based.</summary>
    public const int FirstOrder = 1;

    /// <summary>The path order of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">A bound recipe.</param>
    /// <returns>The order, the branches and the loop edges.</returns>
    public static PathOrder Of(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var walk = new Walk(recipe);
        if (recipe.States.ContainsKey(recipe.Initial))
        {
            walk.Visit(recipe.Initial, branchOf: null);
        }

        return walk.Result(OrderedBy(recipe));
    }

    private static string OrderedBy(Recipe recipe) =>
        recipe.Transitions
            .Where(t => t.From.Contains(recipe.Initial, StringComparer.Ordinal) && t.To != recipe.Initial && recipe.States.ContainsKey(t.To))
            .Select(t => t.To)
            .Distinct(StringComparer.Ordinal)
            .Skip(1)
            .Any()
            ? "transition-list"
            : "path";

    private sealed class Walk(Recipe recipe)
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private readonly HashSet<string> _onWalk = new(StringComparer.Ordinal);
        private readonly List<(string State, string? BranchOf)> _others = [];
        private readonly List<string> _terminals = [];
        private readonly List<PathLoop> _loops = [];

        public void Visit(string state, string? branchOf)
        {
            _ = _seen.Add(state);
            if (recipe.States[state].Kind == StateKind.Terminal)
            {
                _terminals.Add(state);
                return;
            }

            _others.Add((state, branchOf));
            _ = _onWalk.Add(state);
            var descended = false;
            foreach (var target in Targets(state))
            {
                if (_onWalk.Contains(target))
                {
                    AddLoop(state, target);
                }
                else if (!_seen.Contains(target))
                {
                    Visit(target, descended ? branchOf ?? state : branchOf);
                    descended = true;
                }
            }

            _ = _onWalk.Remove(state);
        }

        public PathOrder Result(string orderedBy)
        {
            var places = new List<StatePlace>();
            var order = StatePathOrder.FirstOrder;
            foreach (var (state, branchOf) in _others)
            {
                places.Add(new StatePlace(state, branchOf is null ? order++ : null, branchOf));
            }

            places.AddRange(_terminals.Select(t => new StatePlace(t, order++, BranchOf: null)));
            places.AddRange(recipe.States.Keys.Where(s => !_seen.Contains(s)).Select(s => new StatePlace(s, Order: null, BranchOf: null)));
            return new PathOrder(orderedBy, places, _loops);
        }

        private IEnumerable<string> Targets(string state) =>
            recipe.Transitions
                .Where(t => t.From.Contains(state, StringComparer.Ordinal) && recipe.States.ContainsKey(t.To))
                .Select(t => t.To);

        private void AddLoop(string from, string to)
        {
            if (!_loops.Exists(l => l.From == from && l.To == to))
            {
                _loops.Add(new PathLoop(recipe.States[from].Loop ?? recipe.States[to].Loop, from, to));
            }
        }
    }
}
