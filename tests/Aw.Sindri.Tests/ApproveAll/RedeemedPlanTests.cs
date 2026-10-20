using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>A plan whose challenge was redeemed is the only thing the shipped-recipe approval takes (setup-once spec §5.3).</summary>
[Lane(TestLane.Unit)]
public sealed class RedeemedPlanTests
{
    private readonly SliceHarness _slice = new();
    private static readonly CommandId _batch = CommandId.From(new Guid(192, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    private readonly ShippedRecipePlan _plan = new([Row()], []);

    private static ApproveAllRow Row() =>
        new(RecipeId.From("workflow.step"), "One step", 1, RecipeHash.From("sha256:" + new string('a', 64)), PreviousApprovedHash: null, PlanChange.New, "New", [], CapabilityDelta.None, [], []);

    private ChallengeAnswer Issue(string digest, string typed)
    {
        var issued = _slice.Challenges.Issue(ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest));
        return new ChallengeAnswer(issued.Id, typed);
    }

    [Fact]
    public void A_redeemed_plan_cannot_be_constructed_any_other_way() =>
        Assert.Empty(typeof(RedeemedPlan).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    [Fact]
    public void The_challenge_redeems_for_its_digest_typed_exactly()
    {
        var redeemed = RedeemedPlan.Redeem(_slice.Challenges, Issue(_plan.Digest, "approve-all " + _plan.Digest), _plan, _batch);

        Assert.Same(_plan, redeemed.Value.Plan);
        Assert.Equal(_batch, redeemed.Value.Batch);
        Assert.Null(redeemed.Value.HeldMs);
    }

    [Fact]
    public void Another_text_is_the_recipe_challenge_refusal()
    {
        var redeemed = RedeemedPlan.Redeem(_slice.Challenges, Issue(_plan.Digest, "approve-all 000000"), _plan, _batch);

        Assert.Equal(WorkflowErrors.RecipeChallengeStale.Code, redeemed.Error.Code);
    }

    [Fact]
    public void A_plan_that_moved_since_the_challenge_was_issued_is_refused()
    {
        var redeemed = RedeemedPlan.Redeem(_slice.Challenges, Issue("7b20de", "approve-all 7b20de"), _plan, _batch);

        Assert.Equal(WorkflowErrors.RecipeChallengeStale.Code, redeemed.Error.Code);
    }

    [Fact]
    public void The_challenge_redeems_once()
    {
        var answer = Issue(_plan.Digest, "approve-all " + _plan.Digest);

        Assert.True(RedeemedPlan.Redeem(_slice.Challenges, answer, _plan, _batch).IsSuccess);
        Assert.True(RedeemedPlan.Redeem(_slice.Challenges, answer, _plan, _batch).IsError);
    }
}
