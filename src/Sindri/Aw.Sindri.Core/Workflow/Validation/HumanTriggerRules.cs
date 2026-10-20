using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// RCP-HUMAN-001 (PB spec W23): an effect whose descriptor says <c lang="csharp">HumanTrigger</c> acts with a human's authority, so it
/// may sit only on a transition a human drives: (a) a 'command.&lt;verb&gt;' transition, (b) a 'step.completed' out of
/// human-only Steps, or (c) a transition whose 'when' requires a fact present that only a state entered by (a)
/// transitions carrying a human-trigger effect produces (the click's authority, carried). Pure.
/// </summary>
internal sealed class HumanTriggerRules : IRecipeRules
{
    private const string CommandPrefix = "command.";

    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return Findings(document, context.Catalog);
    }

    private static IEnumerable<RecipeFinding> Findings(RecipeDocument document, BlockCatalog catalog)
    {
        var recipe = document.Recipe;
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var transition = recipe.Transitions[i];
            if (HumanDriven(transition, recipe, catalog))
            {
                continue;
            }

            for (var j = 0; j < transition.Effects.Count; j++)
            {
                if (!IsHumanTrigger(transition.Effects[j], catalog))
                {
                    continue;
                }

                yield return document.Source.Finding(
                    "RCP-HUMAN-001",
                    $"transitions[{Inv(i)}].effects[{Inv(j)}].use",
                    $"effect '{transition.Effects[j].Use.Value}' acts with a human's authority, but transitions[{Inv(i)}] ({string.Join(", ", transition.From)} -> {transition.To} on {transition.On}) isn't a human's act",
                    "put it on a command.<verb> transition, a step.completed out of a human-only Step, or a transition that requires a fact only such a transition produces");
            }
        }
    }

    private static bool IsHumanTrigger(BlockUse effect, BlockCatalog catalog) => catalog.Find(effect.Use) is { Kind: BlockKind.Effect, HumanTrigger: true };

    private static bool HumanDriven(RecipeTransition transition, Recipe recipe, BlockCatalog catalog) =>
        transition.On.StartsWith(CommandPrefix, StringComparison.Ordinal)
        || (string.Equals(transition.On, "step.completed", StringComparison.Ordinal) && transition.From.All(s => HumanOnly(recipe, s, catalog)))
        || (transition.When is { } when && RequiredPresent(when).Any(fact => Carried(fact, recipe, catalog)));

    private static bool HumanOnly(Recipe recipe, string state, BlockCatalog catalog) =>
        recipe.States.TryGetValue(state, out var s) && s.Step is { } step && catalog.Find(step.Use) is { HumanOnly: true };

    /// <summary>The facts a 'when' requires present: its own, or any inside an 'all'.</summary>
    private static IEnumerable<FactKey> RequiredPresent(WhenClause when)
    {
        if (when is { Fact: { } fact, Is: FactPresence.Present })
        {
            yield return fact;
        }

        foreach (var inner in when.All ?? [])
        {
            foreach (var nested in RequiredPresent(inner))
            {
                yield return nested;
            }
        }
    }

    /// <summary>True when something produces <paramref name="fact"/> and every producer leaves only states a human's command enters.</summary>
    private static bool Carried(FactKey fact, Recipe recipe, BlockCatalog catalog)
    {
        var producers = recipe.Transitions.Where(t => t.Produces.Any(p => p.Fact == fact)).ToList();
        return producers.Count > 0 && producers.TrueForAll(p => p.From.All(s => EnteredOnlyByHumanCommands(s, recipe, catalog)));
    }

    private static bool EnteredOnlyByHumanCommands(string state, Recipe recipe, BlockCatalog catalog)
    {
        if (string.Equals(state, recipe.Initial, StringComparison.Ordinal))
        {
            return false;
        }

        var entering = recipe.Transitions.Where(t => string.Equals(t.To, state, StringComparison.Ordinal)).ToList();
        return entering.Count > 0
            && entering.TrueForAll(t => t.On.StartsWith(CommandPrefix, StringComparison.Ordinal) && t.Effects.Any(e => IsHumanTrigger(e, catalog)));
    }

    private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);
}
