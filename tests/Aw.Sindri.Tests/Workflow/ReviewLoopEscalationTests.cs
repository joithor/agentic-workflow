using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The review and design lens loops never ask a human by default (owner, 2026-10-19). The only way to a human is the typed 'needsOwner' outcome, which only an
/// agent's typed escalation produces; a cap hit or no progress keeps its route (review: re-check, then 'stuck', a failure to park; design: the lens desk).
/// </summary>
public sealed class ReviewLoopEscalationTests
{
    private static readonly string[] _escalationEvals = ["two-rounds-no-human", "ambiguous-fix-escalates", "product-question-escalates", "untyped-please-decide-stays"];

    private static Recipe Load(string file) => RecipeComposition.ReadClean(ShippedRecipes.Read($"sindri/{file}"), file).Recipe;

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.review-loop.yaml", "resolve")]
    [InlineData("workflow.design.yaml", "revise")]
    public void The_ask_state_is_the_only_human_wait_and_the_fixer_gets_the_answer(string file, string fixer)
    {
        var recipe = Load(file);
        var catalog = FixtureCatalog.Shipped();

        var human = recipe.States.Where(s => s.Value.Step is { } step && catalog.Find(step.Use)?.HumanOnly == true).Select(static s => s.Key).Order(StringComparer.Ordinal);

        Assert.Equal(file.Contains("design", StringComparison.Ordinal) ? ["ask", "lens-desk"] : ["ask"], human, StringComparer.Ordinal);
        Assert.Equal("step.lens.ask", recipe.States["ask"].Step!.Use.Value);
        Assert.Contains(recipe.Transitions, t => t.From.Contains("ask", StringComparer.Ordinal) && t.To == fixer && t.When?.Outcome?.Value == "answered");
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.review-loop.yaml", "stuck")]
    [InlineData("workflow.design.yaml", "dropped")]
    public void Only_the_needsOwner_outcome_leads_to_ask_and_stop_ends_the_loop(string file, string ended)
    {
        var recipe = Load(file);

        var into = recipe.Transitions.Where(static t => t.To == "ask").ToList();

        Assert.NotEmpty(into);
        Assert.All(into, static t => Assert.True(NeedsOwner(t)));
        Assert.Contains(recipe.Transitions, t => t.From.Contains("ask", StringComparer.Ordinal) && t.To == ended && t.When?.Outcome?.Value == "stop");
    }

    private static bool NeedsOwner(RecipeTransition t) => t.When?.Outcome?.Value == "needsOwner" || t.When?.All?.Any(static c => c.Outcome?.Value == "needsOwner") == true;

    [Fact, Trait("Lane", "unit")]
    public void Both_reviewers_and_the_review_fixer_can_escalate_and_the_loop_caps_the_asks()
    {
        var recipe = Load("workflow.review-loop.yaml");

        Assert.Contains(recipe.Transitions, static t => t.From.Contains("lens-log", StringComparer.Ordinal) && t.To == "ask");
        Assert.Contains(recipe.Transitions, static t => t.From.Contains("fix", StringComparer.Ordinal) && t.To == "ask");
        Assert.Contains(recipe.Transitions, static t => t.From.Contains("resolve", StringComparer.Ordinal) && t.To == "ask");
        Assert.Equal("asks", recipe.States["ask"].Loop);
        Assert.True(recipe.Loops["asks"].Cap > 0);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_cap_and_no_progress_routes_never_lead_to_ask()
    {
        var recipe = Load("workflow.review-loop.yaml");

        Assert.DoesNotContain(recipe.Transitions, static t => t.To == "ask" && t.From.Contains("recheck", StringComparer.Ordinal));
        Assert.Contains(recipe.Transitions, static t => t.From.Contains("recheck", StringComparer.Ordinal) && t.To == "stuck" && t.When?.Outcome?.Value == "findings");
        Assert.Contains(recipe.Transitions, static t => t.From.Contains("fix", StringComparer.Ordinal) && t.To == "recheck");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_review_loop_stays_without_a_human_when_nothing_is_escalated()
    {
        var evals = Load("workflow.review-loop.yaml").Evals.Select(static e => Path.GetFileNameWithoutExtension(e)).ToList();

        Assert.Empty(_escalationEvals.Except(evals, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_ask_Step_asks_the_agents_question_and_answers_flow_to_the_fixer()
    {
        var ask = FixtureCatalog.Shipped().Find(BlockId.From("step.lens.ask"))!;

        Assert.Contains(ask.Inputs, static i => i.Value == "fact:escalation");
        Assert.Equal(["answered", "stop"], ask.Outcomes.Select(static o => o.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
