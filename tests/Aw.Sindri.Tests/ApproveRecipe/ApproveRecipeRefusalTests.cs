using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// Refused approvals (engine spec §4.2, §6.2, §8): a stale challenge is SND-RECIPE-009, a caller other than a human at a
/// terminal is API's SND-AUTH-002, a revoked hash is SND-RECIPE-005, a recipe without evals is SND-RECIPE-008. Each
/// records no 'recipe.approved' and no approval row.
/// </summary>
public sealed class ApproveRecipeRefusalTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Mistyped_action_is_009_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var issued = (await r.Issue("approve workflow.step 4")).Value;

        var refused = await Approve(r, step, issued.Challenge, "approve workflow.step");

        await AssertNothingRecorded(r, step, refused, WorkflowErrors.RecipeChallengeStale.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Challenge_for_another_action_is_009_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var other = r.Challenges.Issue(ChallengeAction.Approve(step.Id, 5), ChallengeSubject.ForRecipe(step.Hash, 4, pinsDigest: null));

        var refused = await Approve(r, step, other.Id, other.Action.Text);

        await AssertNothingRecorded(r, step, refused, WorkflowErrors.RecipeChallengeStale.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Expired_challenge_is_009_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var issued = (await r.Issue("approve workflow.step 4")).Value;
        r.H.Clock.Advance(IssuedChallenge.Lifetime);

        var refused = await Approve(r, step, issued.Challenge, issued.Action);

        await AssertNothingRecorded(r, step, refused, WorkflowErrors.RecipeChallengeStale.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Challenge_whose_subject_changed_is_009_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var moved = r.Challenges.Issue(ChallengeAction.Approve(step.Id, 4), ChallengeSubject.ForRecipe(RegistryData.Hash("an edited file"), 4, pinsDigest: null));

        var refused = await Approve(r, step, moved.Id, moved.Action.Text);

        await AssertNothingRecorded(r, step, refused, WorkflowErrors.RecipeChallengeStale.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_gets_SND_AUTH_002_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var issued = (await r.Issue("approve workflow.step 4")).Value;

        var refused = await r.Run<ApproveRecipeRequest, ApproveRecipeResponse>(
            new ApproveRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(issued.Challenge, issued.Action)), A.Caller().Session().Build());

        await AssertNothingRecorded(r, step, refused, SindriErrors.AuthRefused.Code);
        Assert.Contains("ApproveRecipe is a human decision", refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_app_is_let_through_the_table_and_still_needs_a_challenge_to_approve()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var app = A.Caller().Human().Client(Core.Commands.ClientLabel.Ui).Build();

        var unanswered = await r.Run<ApproveRecipeRequest, ApproveRecipeResponse>(
            new ApproveRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(ChallengeId.For(new Guid(0x77, 0, 0, new byte[8])), "approve workflow.step 4")), app);

        await AssertNothingRecorded(r, step, unanswered, WorkflowErrors.RecipeChallengeStale.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Human_trigger_on_revoked_recipe_gets_005_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approvals.Approve(RegistryData.Approval(step), Token);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step), Token);
        var stale = r.Challenges.Issue(ChallengeAction.Approve(step.Id, 4), ChallengeSubject.ForRecipe(step.Hash, 4, pinsDigest: null));

        var refused = await Approve(r, step, stale.Id, stale.Action.Text);

        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, refused.Error.Code);
        Assert.Empty(r.Events(EventTypes.RecipeApproved));
        _ = Assert.Single((await r.Approvals.History(step.Id, 4, step.Hash, Token)).Value);
        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, (await r.Issue("approve workflow.step 4")).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Recipe_without_evals_is_008_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var bare = await r.Register(RegistryHarness.Parse(step) with { Id = RecipeId.From("workflow.bare"), Evals = [] });

        var refused = await r.Issue("approve workflow.bare 4");

        Assert.Equal(WorkflowErrors.RecipeEvalsFailing.Code, refused.Error.Code);
        Assert.Contains("declares no evals", refused.Error.Message, StringComparison.Ordinal);
        Assert.Null((await r.Approvals.Latest(bare.Id, 4, bare.Hash, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Generated_recipe_whose_base_was_revoked_is_005_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approvals.Approve(RegistryData.Approval(step), Token);
        var fast = RegistryData.Recipe("workflow.fast", baseRecipe: new RecipeRef(step.Id, step.Hash));
        _ = await r.Recipes.Add(fast, Token);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step), Token);   // registered while its base held; the base was revoked since

        var refused = await r.Issue("approve workflow.fast 1");

        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, refused.Error.Code);
        Assert.Contains("base workflow.step@", refused.Error.Message, StringComparison.Ordinal);
        Assert.Null((await r.Approvals.Latest(fast.Id, 1, fast.Hash, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Child_approved_between_preview_and_answer_is_009_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);
        var item = await r.Shipped("sindri/workflow.item.yaml");
        var issued = (await r.Issue("approve workflow.item 3")).Value;   // the preview pins workflow.step@<step>
        var newer = RegistryData.Recipe("workflow.step", version: 5);
        _ = await r.Recipes.Add(newer, Token);
        _ = await r.Approvals.Approve(RegistryData.Approval(newer), Token);   // a second terminal approves a newer child meanwhile

        var refused = await Approve(r, item, issued.Challenge, issued.Action);

        Assert.Equal(WorkflowErrors.RecipeChallengeStale.Code, refused.Error.Code);
        Assert.Null((await r.Approvals.Latest(item.Id, 3, item.Hash, Token)).Value);
    }

    private static async Task<RegistryHarness> Started()
    {
        var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        return r;
    }

    private static Task<Kernel.Result<ApproveRecipeResponse>> Approve(RegistryHarness r, RegisteredRecipe recipe, ChallengeId challenge, string typed) =>
        r.Run<ApproveRecipeRequest, ApproveRecipeResponse>(new ApproveRecipeRequest(recipe.Id, recipe.Hash, new ChallengeAnswer(challenge, typed)), RegistryHarness.Human);

    private static async Task AssertNothingRecorded(RegistryHarness r, RegisteredRecipe recipe, Kernel.Result<ApproveRecipeResponse> refused, string code)
    {
        Assert.Equal(code, refused.Error.Code);
        Assert.Empty(r.Events(EventTypes.RecipeApproved));
        Assert.Null((await r.Approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, Token)).Value);
    }
}
