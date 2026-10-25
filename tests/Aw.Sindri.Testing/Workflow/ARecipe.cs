using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>Builds long-form recipes for tests.</summary>
public static class ARecipe
{
    /// <summary>
    /// The smallest valid long form: one desk Step 'produce' (step.produce, a 4h park timeout) that goes to the
    /// terminal 'done' (resolved) on outcome 'done'. It takes one input, 'fact:item' (RCP-INPUT-002).
    /// </summary>
    /// <param name="id">The recipe's id.</param>
    /// <returns>The recipe.</returns>
    public static Recipe Minimal(RecipeId id) => new(
        Schema: 1,
        Id: id,
        Version: 1,
        Description: null,
        Base: null,
        Urgency: "work",
        Name: null,
        Inputs: [new RecipeInput(FactKey.From("fact:item"), Label: null, Type: "item", Kind: "item", Generate: null)],
        Outputs: [],
        Initial: "produce",
        States: new Dictionary<string, RecipeState>(StringComparer.Ordinal)
        {
            ["produce"] = new(StateKind.Active, new StepUse(BlockId.From("step.produce"), NoArguments, []), WaitFor: null,
                new TimeoutSpec(RecipeDuration.From("4h"), TimeoutAction.Park), Loop: null, Budget: null, Next: "Produce the Step output.", Outcome: null),
            ["done"] = new(StateKind.Terminal, Step: null, WaitFor: null, Timeout: null, Loop: null, Budget: null, Next: null, OutcomeName.From("resolved")),
        },
        Transitions:
        [
            new(["produce"], "done", "step.completed", When(OutcomeName.From("done")), [], [], [], []),
        ],
        Loops: new Dictionary<string, LoopSpec>(StringComparer.Ordinal),
        Budget: null,
        Evals: [],
        Hash: null,
        DisplayName: "Produce and finish");

    /// <summary><paramref name="recipe"/> with each of its Steps naming the input blocks <paramref name="inputs"/>, in order.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="inputs">The input block ids ('input.…').</param>
    /// <returns>The recipe.</returns>
    public static Recipe WithStepInputs(Recipe recipe, params string[] inputs)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var uses = inputs.Select(i => new BlockUse(BlockId.From(i), NoArguments)).ToList();
        return recipe with
        {
            States = recipe.States.ToDictionary(
                static s => s.Key,
                s => s.Value.Step is { } step ? s.Value with { Step = step with { Inputs = uses } } : s.Value,
                StringComparer.Ordinal),
        };
    }

    /// <summary>A '{ outcome }' condition.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The condition.</returns>
    public static WhenClause When(OutcomeName outcome) =>
        new(outcome, Fact: null, Is: null, Loop: null, Below: null, All: null, Any: null, Not: null);

    private static Dictionary<string, WithValue> NoArguments => new(StringComparer.Ordinal);
}
