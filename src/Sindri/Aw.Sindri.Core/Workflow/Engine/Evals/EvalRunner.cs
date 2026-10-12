using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// 'recipe eval' (spec §9.2): folds the engine's decisions over a case's triggers, with the line's gate verdicts as
/// the gate answers and every block treated as having a runtime, and reports the first line that diverges. Pure: it
/// runs 'TransitionDecision' in memory (spec Phase 3); from P4 the interpreter on the time-skipping server runs the
/// same cases, and the two must agree.
/// </summary>
public static class EvalRunner
{
    /// <summary>Runs <paramref name="evalCase"/> against <paramref name="recipe"/>.</summary>
    /// <param name="evalCase">The case.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">The build's descriptors (outcomes, rungs).</param>
    /// <param name="children">The recipe's child recipes, for a child wait.</param>
    /// <returns>Passed, or the first line that diverged.</returns>
    public static EvalVerdict Run(EvalCase evalCase, Recipe recipe, BlockCatalog catalog, IChildRecipeResolver children) =>
        Replay(evalCase, recipe, catalog, children).Verdict;

    /// <summary>
    /// <see cref="Run"/>, with the instance's history up to the line that diverged (the history
    /// <see cref="EvalRecorder"/> turns back into a case).
    /// </summary>
    internal static (EvalVerdict Verdict, IReadOnlyList<InstanceChange> History) Replay(EvalCase evalCase, Recipe recipe, BlockCatalog catalog, IChildRecipeResolver children)
    {
        ArgumentNullException.ThrowIfNull(evalCase);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(children);
        var started = EvalSimulation.Start(evalCase, recipe, catalog, children);
        if (started.IsError)
        {
            return (new EvalVerdict(Passed: false, 1, started.Error.Message), []);
        }

        var simulation = started.Value;
        for (var i = 0; i < evalCase.Steps.Count; i++)
        {
            var line = i + 2;
            var step = simulation.Step(evalCase.Steps[i], line);
            if (step.IsError)
            {
                return (new EvalVerdict(Passed: false, line, step.Error.Message), simulation.History);
            }

            simulation = step.Value.Next;
            if (simulation.Diverges(evalCase.Steps[i].Expect) is { } detail)
            {
                return (new EvalVerdict(Passed: false, line, detail), simulation.History);
            }
        }

        return (new EvalVerdict(Passed: true, Line: null, Detail: null), simulation.History);
    }
}
