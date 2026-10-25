using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>A taken plan is the only thing the shipped-recipe approval takes (setup-once spec §5.3); no typed challenge stands behind it (owner decision q93).</summary>
[Lane(TestLane.Unit)]
public sealed class RedeemedPlanTests
{
    private static readonly CommandId _batch = CommandId.From(new Guid(192, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    private readonly ShippedRecipePlan _plan = new([Row()], []);

    private static ApproveAllRow Row() =>
        new(RecipeId.From("workflow.step"), "One step", 1, RecipeHash.From("sha256:" + new string('a', 64)), PreviousApprovedHash: null, PlanChange.New, "New", [], CapabilityDelta.None, [], []);

    [Fact]
    public void A_redeemed_plan_cannot_be_constructed_any_other_way() =>
        Assert.Empty(typeof(RedeemedPlan).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    [Fact]
    public void The_plan_is_taken_with_the_batch_of_its_start()
    {
        var taken = RedeemedPlan.For(_plan, _batch);

        Assert.Same(_plan, taken.Plan);
        Assert.Equal(_batch, taken.Batch);
    }
}
