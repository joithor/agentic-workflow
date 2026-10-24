using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// An agent approves a recipe when its evals pass against that hash (owner q93, 2026-10-23; RC-01, RC-02): no challenge, the eval files ride
/// with the request and the server runs them, and the approval and its event name the agent. These run the service directly: which caller
/// class reaches the command is api's table.
/// </summary>
public sealed class ApproveRecipeByAgentTests
{
    private static readonly CancellationToken _token = TestContext.Current.CancellationToken;

    private static readonly string[] _stepEvals = ["happy", "no-progress", "rounds"];

    private static CallerContext Agent { get; } = A.Caller().Session().Client(ClientLabel.Mcp).Build();

    [Fact, Trait("Lane", "unit")]
    public async Task An_agent_approves_a_valid_recipe_whose_evals_pass_and_the_approval_names_it()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var approved = await r.Direct(new ApproveRecipeRequest(step.Id, step.Hash, Answer: null, StepEvals()), Agent);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        Assert.Equal(Agent.Actor, approved.Value.Approval.Approver);
        Assert.StartsWith("session:", approved.Value.Approval.Approver, StringComparison.Ordinal);
        var recorded = Assert.Single(r.Events(EventTypes.RecipeApproved));
        Assert.Equal(Agent.Actor, recorded.Envelope.Actor);
        Assert.Equal(approved.Value.Approval, (await r.Approvals.Latest(step.Id, step.Version, step.Hash, _token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_agent_is_refused_when_an_eval_fails_and_nothing_is_recorded()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var evals = StepEvals().Select(e => e.Path.EndsWith("happy.jsonl", StringComparison.Ordinal) ? e with { Text = e.Text + "\nnot a line of an eval case\n" } : e).ToList();

        var refused = await r.Direct(new ApproveRecipeRequest(step.Id, step.Hash, Answer: null, evals), Agent);

        Assert.Equal(WorkflowErrors.RecipeEvalsFailing.Code, refused.Error.Code);
        Assert.Contains("happy.jsonl", refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty(r.Events(EventTypes.RecipeApproved));
        Assert.Null((await r.Approvals.Latest(step.Id, step.Version, step.Hash, _token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_agent_that_leaves_an_eval_file_out_is_refused()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var refused = await r.Direct(new ApproveRecipeRequest(step.Id, step.Hash, Answer: null, [.. StepEvals().Skip(1)]), Agent);

        Assert.Equal(WorkflowErrors.RecipeEvalsFailing.Code, refused.Error.Code);
        Assert.Contains("didn't carry", refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty(r.Events(EventTypes.RecipeApproved));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_agent_approves_a_recipe_that_lists_no_evals_on_its_validation_alone()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var bare = await r.Register(RegistryHarness.Parse(step) with { Id = RecipeId.From("workflow.bare"), Evals = [] });

        var approved = await r.Direct(new ApproveRecipeRequest(bare.Id, bare.Hash), Agent);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        Assert.Equal(Agent.Actor, approved.Value.Approval.Approver);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_still_needs_the_challenge_and_its_typed_text()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var bare = await r.Direct(new ApproveRecipeRequest(step.Id, step.Hash), RegistryHarness.Human);
        var wrong = await r.Direct(new ApproveRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(r.Challenges.Issue(ChallengeAction.Approve(step.Id, step.Version), ChallengeSubject.ForRecipe(step.Hash, step.Version, pinsDigest: null)).Id, "wrong text")), RegistryHarness.Human);

        Assert.False(bare.IsSuccess);
        Assert.False(wrong.IsSuccess);
        Assert.Empty(r.Events(EventTypes.RecipeApproved));
    }

    private static List<EvalFile> StepEvals() =>
        [.. _stepEvals.Select(name => new EvalFile($"evals/workflow.step/{name}.jsonl", ShippedRecipes.Read($"sindri/evals/workflow.step/{name}.jsonl")))];
}
