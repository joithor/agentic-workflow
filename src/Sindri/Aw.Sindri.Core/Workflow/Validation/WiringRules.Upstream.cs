using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-WIRE-001 (W2): every fact a block, a 'with:' value or a child input needs is there on every path into where it runs.</summary>
internal sealed partial class WiringRules
{
    /// <summary>The recipe and catalog a block's needs are read against.</summary>
    private sealed record NeedContext(Recipe Recipe, BlockCatalog Catalog);

    /// <summary>A fact something needs, and where.</summary>
    /// <param name="Path">The node that needs it.</param>
    /// <param name="What">How the finding names the needer: "step 'step.checks' in state 'checks'".</param>
    /// <param name="Fact">The fact.</param>
    /// <param name="State">The state it is needed in, or the state a transition leaves.</param>
    /// <param name="Transition">The transition it runs on, whose 'produces' it may also read; null for a state's own needs.</param>
    private sealed record Need(string Path, string What, FactKey Fact, string State, RecipeTransition? Transition);

    private static IEnumerable<RecipeFinding> Upstream(Recipe recipe, BlockCatalog catalog, RecipeSource source)
    {
        var available = FactsReaching.Of(recipe);
        return StateNeeds(recipe, catalog)
            .Concat(TransitionNeeds(recipe, catalog))
            .Where(n => available.TryGetValue(n.State, out var facts) && !facts.Contains(n.Fact) && !(n.Transition?.Produces.Any(p => p.Fact == n.Fact) ?? false))
            .DistinctBy(n => (n.Path, n.Fact))
            .Select(n => source.Finding(
                "RCP-WIRE-001",
                n.Path,
                $"{n.What} needs '{n.Fact.Value}', but {FactsReaching.Witness(recipe, available, n.State, n.Fact)}",
                n.Transition is null
                    ? $"produce it on every path into '{n.State}', or add it to the recipe's inputs"
                    : $"produce it on this transition or on every path into '{n.State}', or add it to the recipe's inputs"));
    }

    private static IEnumerable<Need> StateNeeds(Recipe recipe, BlockCatalog catalog) =>
        recipe.States.SelectMany(s => StepNeeds(recipe, s.Key, s.Value.Step, catalog).Concat(ChildNeeds(s.Key, s.Value.WaitFor?.Child)));

    private static IEnumerable<Need> StepNeeds(Recipe recipe, string state, StepUse? step, BlockCatalog catalog)
    {
        if (step is null)
        {
            return [];
        }

        var where = $"in state '{state}'";
        var inputs = step.Inputs.SelectMany((input, k) => BlockNeeds($"states.{state}.step.inputs[{Inv(k)}]", "input", input, new NeedContext(recipe, catalog), where));
        return BlockNeeds($"states.{state}.step", "step", new BlockUse(step.Use, step.With), new NeedContext(recipe, catalog), where)
            .Concat(inputs)
            .Select(need => need with { State = state });
    }

    private static IEnumerable<Need> ChildNeeds(string state, ChildWait? child) =>
        (child?.Inputs ?? new Dictionary<FactKey, WithValue>())
            .Where(i => i.Value.Fact is not null)
            .Select(i => new Need(
                $"states.{state}.waitFor.child.inputs.{i.Key.Value}", $"child input '{i.Key.Value}' in state '{state}'", i.Value.Fact.GetValueOrDefault(), state,
                Transition: null));

    private static IEnumerable<Need> TransitionNeeds(Recipe recipe, BlockCatalog catalog)
    {
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var transition = recipe.Transitions[i];
            var on = $"on transitions[{Inv(i)}]";
            var needs = transition.Gates
                .SelectMany((g, j) => BlockNeeds($"transitions[{Inv(i)}].gates[{Inv(j)}]", "gate", new BlockUse(g.Use, g.With), new NeedContext(recipe, catalog), on))
                .Concat(transition.Effects.SelectMany((e, j) => BlockNeeds($"transitions[{Inv(i)}].effects[{Inv(j)}]", "effect", e, new NeedContext(recipe, catalog), on)))
                .Concat(transition.Evidence.SelectMany((e, j) => EvidenceNeeds($"transitions[{Inv(i)}].evidence[{Inv(j)}]", e.Kind, catalog, on)))
                .ToList();
            foreach (var from in transition.From.Distinct(StringComparer.Ordinal))
            {
                foreach (var need in needs)
                {
                    yield return need with { State = from, Transition = transition };
                }
            }
        }
    }

    /// <summary>
    /// A block's declared inputs, at its 'use', and the facts its 'with:' values read. The state is filled in by the
    /// caller. A block of the wrong kind for its place (a Step named as a gate) is RCP-BLOCK-002's, so its inputs aren't read.
    /// </summary>
    private static IEnumerable<Need> BlockNeeds(string path, string word, BlockUse use, NeedContext context, string where)
    {
        var what = $"{word} '{use.Use.Value}' {where}";
        var inputs = context.Catalog.Find(use.Use) is { } descriptor && descriptor.Kind == KindOf(word)
            ? (word == "step" ? StepInputs.Of(descriptor, use.With, context.Recipe) : descriptor.Inputs) ?? []   // a choice Step's facts come from its parameters; a list that doesn't resolve is RCP-CHOICE-004's
            : [];
        var declared = inputs.Select(fact => new Need($"{path}.use", what, fact, State: string.Empty, Transition: null));
        var read = use.With
            .Where(w => w.Value.Fact is not null)
            .Select(w => new Need($"{path}.with.{w.Key}", $"parameter '{w.Key}' of {what}", w.Value.Fact.GetValueOrDefault(), State: string.Empty, Transition: null));
        return declared.Concat(read);
    }

    private static BlockKind KindOf(string word) => word switch
    {
        "step" => BlockKind.Step,
        "input" => BlockKind.StepInput,
        "gate" => BlockKind.Gate,
        _ => BlockKind.Effect,
    };

    private static IEnumerable<Need> EvidenceNeeds(string path, string kind, BlockCatalog catalog, string where) =>
        BlockId.TryFrom($"evidence.{kind}", out var id) && catalog.Find(id) is { Kind: BlockKind.EvidenceProvider } provider
            ? provider.Inputs.Select(fact => new Need($"{path}.kind", $"evidence '{kind}' {where}", fact, State: string.Empty, Transition: null))
            : [];
}
