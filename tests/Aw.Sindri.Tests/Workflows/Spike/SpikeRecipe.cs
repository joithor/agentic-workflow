using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// The part of a recipe the test interpreter walks, projected from P1's normalized <see cref="Recipe"/> (the workflow
/// input must serialize with the SDK's default converter, so it carries plain values, not the typed recipe).
/// </summary>
/// <param name="Initial">The initial state.</param>
/// <param name="States">The states.</param>
/// <param name="Transitions">The transitions, in file order.</param>
/// <param name="LoopCaps">Each loop's cap.</param>
internal sealed record SpikeRecipe(string Initial, IReadOnlyList<SpikeState> States, IReadOnlyList<SpikeTransition> Transitions, IReadOnlyDictionary<string, int> LoopCaps)
{
    /// <summary>The state named <paramref name="name"/>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The state.</returns>
    public SpikeState State(string name) => States.First(s => s.Name == name);

    /// <summary>Projects <paramref name="recipe"/>.</summary>
    /// <param name="recipe">P1's normalized recipe.</param>
    /// <returns>The projection.</returns>
    public static SpikeRecipe From(Recipe recipe) => new(
        recipe.Initial,
        [.. recipe.States.Select(s => new SpikeState(s.Key, s.Value.Kind == StateKind.Terminal, s.Value.Timeout?.After.Span ?? TimeSpan.Zero, s.Value.Loop))],
        [.. recipe.Transitions.Select(t => new SpikeTransition(t.From, t.To, t.On, t.When?.Outcome?.Value))],
        recipe.Loops.ToDictionary(l => l.Key, l => l.Value.Cap, StringComparer.Ordinal));
}
