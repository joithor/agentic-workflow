using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>
/// The reusable gate-dominance walker (DT10; line spec App. B.1 'gates'): a recipe in, the state a path must reach and
/// the gate states every path to it has to pass through. A gate dominates a target when the target cannot be reached
/// from the initial state with the gate removed. workflow.phase uses it for prove, review and ship; DT-M reuses it for
/// workflow.bugfix.
/// </summary>
internal static class GateDominance
{
    /// <summary>The gates among <paramref name="gates"/> that some path from the initial state to <paramref name="target"/> skips.</summary>
    /// <param name="recipe">The recipe to walk.</param>
    /// <param name="target">The state every path ends at, for example 'merged'.</param>
    /// <param name="gates">The states every path must pass through.</param>
    /// <returns>The skipped gates, in the order given; empty when each one dominates the target.</returns>
    public static IReadOnlyList<string> Skipped(Recipe recipe, string target, params string[] gates)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(gates);
        Require(recipe, target);
        Assert.True(PathAround(recipe, target, gate: null) is not null, $"{recipe.Id.Value}: no path from '{recipe.Initial}' to '{target}', so dominance would hold vacuously");
        foreach (var gate in gates)
        {
            Require(recipe, gate);
        }

        return [.. gates.Where(gate => PathAround(recipe, target, gate) is not null)];
    }

    /// <summary>One path from the initial state to <paramref name="target"/> that skips <paramref name="gate"/>, or null.</summary>
    /// <param name="recipe">The recipe to walk.</param>
    /// <param name="target">The state the path ends at.</param>
    /// <param name="gate">The state the path avoids; null for no restriction.</param>
    /// <returns>The state names along the path, or null when the gate dominates.</returns>
    public static IReadOnlyList<string>? PathAround(Recipe recipe, string target, string? gate)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (string.Equals(recipe.Initial, gate, StringComparison.Ordinal))
        {
            return null;
        }

        var parent = new Dictionary<string, string?>(StringComparer.Ordinal) { [recipe.Initial] = null };
        var queue = new Queue<string>([recipe.Initial]);
        while (queue.Count > 0)
        {
            var state = queue.Dequeue();
            if (string.Equals(state, target, StringComparison.Ordinal))
            {
                var path = new List<string>();
                var at = (string?)state;
                while (at is not null)
                {
                    path.Add(at);
                    at = parent[at];
                }

                path.Reverse();
                return path;
            }

            foreach (var next in Successors(recipe, state).Where(n => !string.Equals(n, gate, StringComparison.Ordinal) && !parent.ContainsKey(n)))
            {
                parent[next] = state;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static IEnumerable<string> Successors(Recipe recipe, string state) =>
        recipe.Transitions.Where(t => t.From.Contains(state, StringComparer.Ordinal)).Select(static t => t.To).Distinct(StringComparer.Ordinal);

    private static void Require(Recipe recipe, string state) =>
        Assert.True(recipe.States.ContainsKey(state), $"{recipe.Id.Value} has no state '{state}'");
}
