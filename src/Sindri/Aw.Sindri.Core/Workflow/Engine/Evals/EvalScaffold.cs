using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// 'recipe eval --scaffold' (spec §6.3, §9.2): a happy-path case for a new recipe, line 1 marked '"scaffold": true'.
/// From each state it tries the source's declared outcomes in order (a Step's, an event's, the outcomes a child wait's
/// transitions name; an answer wait gets a 'text' answer) and keeps the first whose transition the engine applies into a state not yet visited, until a
/// terminal state or a state nothing moves on from. Every gate passes. Each line's 'expect' is what the engine did, so
/// the printed case passes.
/// </summary>
public static partial class EvalScaffold
{
    private const int MaxLines = 64;

    /// <summary>The happy-path case of <paramref name="recipe"/>; a child wait's child doesn't resolve.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">The build's descriptors (the Steps' declared outcomes and outputs).</param>
    /// <returns>The case, named 'scaffold'.</returns>
    public static EvalCase Generate(Recipe recipe, BlockCatalog catalog) => Generate(recipe, catalog, NoChildren.Instance);

    /// <summary>The happy-path case of <paramref name="recipe"/>, its child waits' children resolved by <paramref name="children"/>.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">The build's descriptors.</param>
    /// <param name="children">The recipe's child recipes.</param>
    /// <returns>The case, named 'scaffold'.</returns>
    public static EvalCase Generate(Recipe recipe, BlockCatalog catalog, IChildRecipeResolver children)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(catalog);
        var inputs = recipe.Inputs.ToDictionary(static i => i.Key, static i => EvalFacts.OfText($"{i.Key.Value} for the scaffold"));
        var start = new EvalStartLine(new EvalStart(inputs), Scaffold: true);
        var steps = new List<EvalStepLine>();
        var started = EvalSimulation.Start(new EvalCase("scaffold", start, []), recipe, catalog, children);
        if (started.IsError)
        {
            return new EvalCase("scaffold", start, steps);
        }

        var simulation = started.Value;
        var visited = new HashSet<string>(StringComparer.Ordinal) { simulation.Instance.State };
        while (steps.Count < MaxLines && !simulation.Instance.Ended && Next(simulation, visited, steps.Count + 2) is var (line, next))
        {
            steps.Add(line with { Expect = next.Shown });
            _ = visited.Add(next.Instance.State);
            simulation = next;
        }

        return new EvalCase("scaffold", start, steps);
    }

    private static (EvalStepLine Line, EvalSimulation Next)? Next(EvalSimulation simulation, HashSet<string> visited, int number)
    {
        foreach (var trigger in Candidates(simulation))
        {
            var line = new EvalStepLine(trigger, new EvalExpectation(string.Empty));
            var step = simulation.Step(line, number);
            if (!step.IsError && step.Value.Decision is Applied && (step.Value.Next.Instance.Ended || !visited.Contains(step.Value.Next.Instance.State)))
            {
                return (line, step.Value.Next);
            }
        }

        return null;
    }

    private static IEnumerable<EvalTrigger> Candidates(EvalSimulation simulation)
    {
        var instance = simulation.Instance;
        if (!simulation.Recipe.States.TryGetValue(instance.State, out var state))
        {
            return [];
        }

        if (state.Step is { } step && simulation.Catalog.Find(step.Use) is { } descriptor)
        {
            return (StepOutcomes.Of(descriptor, step, simulation.Recipe) ?? []).Select(o => new EvalTrigger(TriggerNames.StepCompleted, o, Outputs(descriptor, o), Park: null));
        }

        return state.WaitFor is { } wait ? WaitCandidates(simulation, wait) : [];
    }

    private static IEnumerable<EvalTrigger> WaitCandidates(EvalSimulation simulation, WaitFor wait)
    {
        if (wait.Answer is { } answer)
        {
            var text = new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:answer")] = EvalFacts.OfText($"answer to {answer.Id.Value} for the scaffold") };
            return [new EvalTrigger(TriggerNames.Answer, Outcome: null, text, Park: null, EvalVariant.Text)];
        }

        IEnumerable<OutcomeName?> outcomes = wait.Event is { } kind && simulation.Catalog.FindEvent(kind) is { } e
            ? [.. e.Outcomes, null]
            : [.. simulation.Recipe.Transitions
                .Where(t => t.On == TriggerNames.WaitSatisfied && t.From.Contains(simulation.Instance.State, StringComparer.Ordinal))
                .Select(static t => t.When?.Outcome).Distinct(), null];
        var present = wait.Child is { } child && simulation.Children.Resolve(child.Recipe) is { } found ? ChildOutputs.PresentOn(found.Recipe) : null;
        return outcomes.Distinct().Select(o => new EvalTrigger(TriggerNames.WaitSatisfied, o, ChildLineOutputs(wait.Child, o, present), Park: null));
    }

    /// <summary>The outputs a child always carries on <paramref name="outcome"/>, as placeholder content (null when it carries none).</summary>
    private static Dictionary<FactKey, JsonElement>? ChildLineOutputs(ChildWait? child, OutcomeName? outcome, IReadOnlyDictionary<OutcomeName, IReadOnlySet<FactKey>>? present)
    {
        if (child is null || outcome is not { } reported || present is null || !present.TryGetValue(reported, out var facts) || facts.Count == 0)
        {
            return null;
        }

        return facts.ToDictionary(static f => f, f => EvalFacts.OfText($"{f.Value} from {child.Recipe.Value} ({reported.Value})"));
    }

    /// <summary>The outputs a Step must write on <paramref name="outcome"/>, as placeholder content.</summary>
    private static Dictionary<FactKey, JsonElement>? Outputs(BlockDescriptor descriptor, OutcomeName outcome)
    {
        var required = descriptor.Outputs.Where(o => StepOutputs.CallerWritesOn(o, outcome)).ToDictionary(
            static o => o.Fact, o => EvalFacts.OfText($"{o.Fact.Value} from {descriptor.Id.Value} ({outcome.Value})"));
        return required.Count == 0 ? null : required;
    }

    private sealed class NoChildren : IChildRecipeResolver
    {
        public static NoChildren Instance { get; } = new();

        public ChildRecipe? Resolve(RecipeId id) => null;
    }
}
