using Aw.Sindri.Features.EvalRecipe;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The P3 signal (spec Phase 3, §9.2, §10 'Replay reproduces the instance'): 'sindri recipe eval
/// recipes/sindri/workflow.step.yaml' folds the engine's decisions over each shipped case and prints 'happy pass',
/// 'no-progress pass' and 'rounds pass'.
/// </summary>
public sealed class EvalRecipeTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Eval_cases_reach_their_expected_states()
    {
        var response = (await Service().Handle(new EvalRecipeRequest("sindri/workflow.step.yaml"), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["happy pass", "no-progress pass", "rounds pass"], response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
        Assert.True(response.Passed);
    }

    // P3 unit (f) gives the service its ports and builds it here over the shipped recipes and evals
    // (ShippedRecipes in an InMemoryRecipeFiles), the reader and the shipped blocks' catalog.
    private static EvalRecipeService Service() => new();
}
