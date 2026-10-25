using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A step.completed or step.failed eval line's optional 'tokens' (spec §3.7, §9.2): the tokens the run reported, 0 when
/// left out, so an eval can pin a per-round budget park and the grant that answers it.
/// </summary>
public sealed class EvalTokensTests
{
    private const string Start = """{"start":{"inputs":{}}}""";
    private const string Producing = """{"state":"produce"}""";
    private const string Parked = """{"state":"parked","park":"budget"}""";

    [Fact, Trait("Lane", "unit")]
    public void A_failed_round_past_its_allowance_parks_and_a_grant_answers_it()
    {
        var verdict = Run(
            Budgeted(100_000),
            Line("""{"kind":"step.failed","reason":"the first draft was thin","tokens":139000}""", Parked),
            Line("""{"kind":"answer","variant":"grant-tokens","amount":400000}""", Producing));

        Assert.Null(verdict.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_round_that_stays_inside_its_allowance_goes_on()
    {
        var verdict = Run(
            Budgeted(1_000_000),
            Line("""{"kind":"step.failed","reason":"the first draft was thin","tokens":139000}""", Producing),
            Line("""{"kind":"step.failed","reason":"the second was too","tokens":139000}""", Producing));

        Assert.Null(verdict.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_run_past_the_backstop_parks_on_budget_and_the_grant_resumes_it()
    {
        var verdict = Run(
            Budgeted(1_000_000),
            Line("""{"kind":"step.failed","reason":"too much","tokens":1200000}""", Parked),
            Line("""{"kind":"answer","variant":"grant-tokens","amount":1000000}""", Producing),
            Line("""{"kind":"step.failed","reason":"again","tokens":10}""", Producing));

        Assert.Null(verdict.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_tokens_a_run_spends_nothing()
    {
        var verdict = Run(Budgeted(1), Line("""{"kind":"step.failed","reason":"x"}""", Producing));

        Assert.Null(verdict.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tokens_on_another_kind_fails_its_line()
    {
        var verdict = Run(Budgeted(1_000_000), Line("""{"kind":"takeover","tokens":5}""", """{"state":"produce"}"""));

        Assert.Equal(2, verdict.Line);
        Assert.Contains("'tokens' belongs on a step.completed or step.failed line only", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Negative_tokens_fail_their_line()
    {
        var verdict = Run(Budgeted(1_000_000), Line("""{"kind":"step.failed","tokens":-1}""", Producing));

        Assert.Equal(2, verdict.Line);
        Assert.Contains("'tokens' must be 0 or more", verdict.Detail, StringComparison.Ordinal);
    }

    private static EvalVerdict Run(Recipe recipe, params string[] lines)
    {
        var read = EvalCaseReader.Read("case", string.Join('\n', [Start, .. lines]), EvalComposition.Options);
        return read.Case is { } evalCase ? EvalRunner.Run(evalCase, recipe, EngineFixture.Catalog, InMemoryChildRecipes.None) : read.Invalid!; // why: a reading has a case or why it has none
    }

    private static string Line(string trigger, string expect) => "{\"trigger\":" + trigger + ",\"expect\":" + expect + "}";

    /// <summary>'produce' (loop 'revisions', cap 3, budget <paramref name="tokens"/> per round) goes back into itself on step.failed and to 'done' on step.completed.</summary>
    private static Recipe Budgeted(int tokens)
    {
        var recipe = EngineFixture.Minimal(
            [new RecipeTransition(["produce"], "produce", "step.failed", When: null, [], [], [], []), EngineFixture.From("done")],
            new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["revisions"] = new(3, LoopSpec.DefaultNoProgress) },
            loop: "revisions");
        var states = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal) { ["produce"] = recipe.States["produce"] with { Budget = new BudgetSpec(tokens) } };
        return recipe with { States = states };
    }
}
