using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

public static partial class EvalScaffold
{
    /// <summary>
    /// A case for each path from the start to a terminal state (DT spec §8.1): at a route state (a Step that takes its
    /// outcomes from its own 'choices') it branches over each choice, then 'uncertain', then 'step.failed'; after the desk
    /// twin it takes the first choice; elsewhere it takes the first applicable outcome, as
    /// <see cref="Generate(Recipe, BlockCatalog, IChildRecipeResolver)"/> does.
    /// </summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">The build's descriptors.</param>
    /// <param name="children">The recipe's child recipes.</param>
    /// <returns>The cases, each marked '"scaffold": true': 'scaffold' for a recipe without a route, otherwise 'scaffold-&lt;branch&gt;' for each path (the outcome picked at each route, or 'step-failed').</returns>
    public static IReadOnlyList<EvalCase> GenerateAll(Recipe recipe, BlockCatalog catalog, IChildRecipeResolver children)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(catalog);
        var inputs = recipe.Inputs.ToDictionary(static i => i.Key, static i => EvalFacts.OfText($"{i.Key.Value} for the scaffold"));
        var start = new EvalStartLine(new EvalStart(inputs), Scaffold: true);
        var started = EvalSimulation.Start(new EvalCase("scaffold", start, []), recipe, catalog, children);
        if (started.IsError)
        {
            return [new EvalCase("scaffold", start, [])];
        }

        var paths = new List<(IReadOnlyList<string> Branches, IReadOnlyList<EvalStepLine> Lines)>();
        Explore(started.Value, new Walk([], [], new HashSet<string>(StringComparer.Ordinal) { started.Value.Instance.State }), paths);
        return [.. paths.Select(p => new EvalCase(string.Concat(p.Branches.Select(static b => "-" + b)).Insert(0, "scaffold"), start, p.Lines))];
    }

    /// <summary>The lines taken so far, the branches picked at routes, and the states visited on this path.</summary>
    private sealed record Walk(List<EvalStepLine> Lines, List<string> Branches, HashSet<string> Visited)
    {
        public Walk Fork(EvalStepLine line, EvalSimulation next, string? branch) => new(
            [.. Lines, line with { Expect = next.Shown }],
            branch is null ? [.. Branches] : [.. Branches, branch],
            [.. Visited, next.Instance.State]);
    }

    private static void Explore(EvalSimulation simulation, Walk walk, List<(IReadOnlyList<string> Branches, IReadOnlyList<EvalStepLine> Lines)> paths)
    {
        while (walk.Lines.Count < MaxLines && !simulation.Instance.Ended)
        {
            if (Alternatives(simulation) is { } alternatives)
            {
                foreach (var (branch, trigger) in alternatives)
                {
                    var line = new EvalStepLine(trigger, new EvalExpectation(string.Empty));
                    var step = simulation.Step(line, walk.Lines.Count + 2);
                    if (!step.IsError && step.Value.Decision is Applied && (step.Value.Next.Instance.Ended || !walk.Visited.Contains(step.Value.Next.Instance.State)))
                    {
                        Explore(step.Value.Next, walk.Fork(line, step.Value.Next, branch), paths);
                    }
                }

                return;
            }

            if (Next(simulation, walk.Visited, walk.Lines.Count + 2) is not var (next, after))
            {
                break;
            }

            walk = walk.Fork(next, after, branch: null);
            simulation = after;
        }

        paths.Add((walk.Branches, walk.Lines));
    }

    /// <summary>The triggers a route state branches over, each with its branch name: each outcome in order, then 'step.failed'; null when the state isn't a route.</summary>
    private static List<(string Branch, EvalTrigger Trigger)>? Alternatives(EvalSimulation simulation)
    {
        if (!simulation.Recipe.States.TryGetValue(simulation.Instance.State, out var state) || state.Step is not { } step
            || simulation.Catalog.Find(step.Use) is not { OutcomesFrom: { } from } descriptor || ParameterLists.IsTwin(from)
            || StepOutcomes.Of(descriptor, step, simulation.Recipe) is not { } outcomes)
        {
            return null;
        }

        return
        [
            .. outcomes.Select(o => (o.Value, new EvalTrigger(TriggerNames.StepCompleted, o, Outputs(descriptor, o), Park: null))),
            ("step-failed", new EvalTrigger(TriggerNames.StepFailed)),
        ];
    }
}
