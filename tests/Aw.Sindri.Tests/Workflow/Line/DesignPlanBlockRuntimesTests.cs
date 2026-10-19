using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>
/// The design and plan stations on the block runtime (line spec §4.1, §6.1, BS2): in the shipped build, their deterministic Steps open block runs, not desk tasks,
/// and the real Steps' results are accepted by the engine's report validation and carry the instance to the state the recipe names.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class DesignPlanBlockRuntimesTests
{
    private const string Decision = """{"id":"D1","question":"Where does expiry run?","affects":["sec-1"],"cites":["S1"],"status":"open","options":[{"id":"D1.a","answer":"On read.","tradeoffs":"Exact.","recommended":true},{"id":"D1.b","answer":"Nightly.","tradeoffs":"Cheap."}]}""";

    private const string Design = """{"decisions":[""" + Decision + """]}""";

    [Theory]
    [InlineData("workflow.plan", "check")]
    [InlineData("workflow.plan", "questions")]
    [InlineData("workflow.plan", "settle")]
    [InlineData("workflow.design", "check")]
    public void The_Step_opens_a_block_run_not_a_desk_task(string recipeName, string state)
    {
        using var provider = ShippedComposition.Compose();
        var (recipe, context) = Station(provider, recipeName);

        var entered = StateEntry.Enter(EngineFixture.At(recipe, state), recipe, state, context);

        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
    }

    [Theory]
    [InlineData("""[{"question":"Q1","pick":"a"}]""", "assumed", "stop")]
    [InlineData("""[{"question":"Q1","pick":"b"}]""", "changed", "redesign")]
    [InlineData("""[{"question":"Q-plan","other":"Split P1."}]""", "replan", "graph")]
    public async Task Settle_moves_the_plan_to_the_stop_back_to_design_or_back_to_the_graph(string answers, string outcome, string next)
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Station(provider, "workflow.plan");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "settle"), recipe, "settle", context);
        await using var scope = provider.CreateAsyncScope();

        var run = await scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>("step.plan.settle")
            .Run(ScopingStepContexts.Of("step.plan.settle", new ScopingFacts { ["design"] = Design, ["answers"] = answers, ["answerLog"] = "[]" }), TestContext.Current.CancellationToken);

        Assert.Equal(outcome, run.Result?.Outcome.Value);
        Assert.Equal(next, Next(entered, recipe, context, run), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Questions_asks_the_open_decision_and_the_engine_opens_the_decide_desk()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Station(provider, "workflow.plan");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "questions"), recipe, "questions", context);
        await using var scope = provider.CreateAsyncScope();

        var run = await scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>("step.plan.questions")
            .Run(ScopingStepContexts.Of("step.plan.questions", new ScopingFacts { ["design"] = Design, ["answers"] = "[]", ["answerLog"] = "[]" }), TestContext.Current.CancellationToken);

        Assert.Equal("asked", run.Result?.Outcome.Value);
        Assert.Equal("decide", Next(entered, recipe, context, run), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Design_check_passes_to_the_lens_round_and_a_gap_sends_the_draft_back()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Station(provider, "workflow.design");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "check"), recipe, "check", context);
        await using var scope = provider.CreateAsyncScope();
        var step = scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>("step.design.check");
        const string scopeFact = """{"scopeMap":{"surfaces":[{"id":"S1"}]},"acceptance":{"items":[{"id":"AC-1","text":"WHEN a THEN b."}]},"openQuestions":[],"answerLog":[]}""";
        const string covered = """{"sections":[{"id":"sec-1","covers":["S1"],"cites":["S1"]}],"signals":[{"item":"AC-1","kind":"test","statement":"b holds","runs":"head"}]}""";

        var pass = await step.Run(ScopingStepContexts.Of("step.design.check", new ScopingFacts { ["scope"] = scopeFact, ["packs"] = "{}", ["design"] = covered }), TestContext.Current.CancellationToken);
        var gaps = await step.Run(ScopingStepContexts.Of("step.design.check", new ScopingFacts { ["scope"] = scopeFact, ["packs"] = "{}", ["design"] = covered.Replace("\"covers\":[\"S1\"]", "\"covers\":[]", StringComparison.Ordinal) }), TestContext.Current.CancellationToken);

        Assert.Equal("round", Next(entered, recipe, context, pass), StringComparer.Ordinal);
        Assert.Equal("draft", Next(entered, recipe, context, gaps), StringComparer.Ordinal);
    }

    private static (Recipe Recipe, DecisionContext Context) Station(ServiceProvider provider, string name)
    {
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read($"sindri/{name}.yaml"), $"{name}.yaml").Recipe;
        var runtimes = provider.GetRequiredService<BlockStepRuntimes>().Ids.ToDictionary(static id => id, static _ => StepRuntime.Block);
        return (recipe, new DecisionContext(ApprovalCheck.Holds, provider.GetRequiredService<BlockCatalog>(), runtimes, new InMemoryChildRecipes()));
    }

    // The state the engine moves to on the Step's result, after the same report validation a desk report gets.
    private static string Next(WorkflowInstance entered, Recipe recipe, DecisionContext context, BlockStepResult run)
    {
        var done = run.Result ?? throw new InvalidOperationException(run.Failure);
        var outputs = done.Outputs.ToDictionary(static o => o.Key, static o => EngineFixture.Fact(o.Value.ValueKind == JsonValueKind.String ? o.Value.GetString()! : o.Value.GetRawText()));   // why: a string element always holds its text
        var step = recipe.States[entered.State].Step!.Use;   // why: the caller entered a Step state
        var trigger = BlockStepTriggers.Completed(entered, recipe, context, new BlockCompletion(step, entered.Frame.Run!.Token, done.Outcome, outputs));   // why: a block run is open
        return Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(entered, recipe, trigger, GateAnswers.None, context))).Changes[0].After.State;
    }
}
