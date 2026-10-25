using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>The digest an approve-all challenge is bound to (setup-once spec §5.3).</summary>
[Lane(TestLane.Unit)]
public sealed class ApproveAllDigestTests
{
    private static ApproveAllRow Row(string id, char hash, params string[] capabilities) =>
        new(RecipeId.From(id), id, 1, RecipeHash.From("sha256:" + new string(hash, 64)), PreviousApprovedHash: null, PlanChange.New, "New",
            [.. capabilities.Select(static c => new CapabilityLine(CapabilityId.From(c), c))], CapabilityDelta.None, [], []);

    [Fact]
    public void An_empty_plan_has_the_empty_digest() => Assert.Equal("000000", ApproveAllDigest.Of([]), StringComparer.Ordinal);

    [Fact]
    public void The_digest_is_six_lowercase_hex_characters() =>
        Assert.Matches("^[0-9a-f]{6}$", ApproveAllDigest.Of([Row("workflow.step", 'a', "step.produce")]));

    [Fact]
    public void A_capability_set_has_no_order_but_the_recipes_do()
    {
        var one = ApproveAllDigest.Of([Row("workflow.step", 'a', "step.produce", "effect.notify.desk"), Row("workflow.item", 'b')]);
        var capabilitiesReordered = ApproveAllDigest.Of([Row("workflow.step", 'a', "effect.notify.desk", "step.produce"), Row("workflow.item", 'b')]);
        var recipesReordered = ApproveAllDigest.Of([Row("workflow.item", 'b'), Row("workflow.step", 'a', "step.produce", "effect.notify.desk")]);

        Assert.Equal(one, capabilitiesReordered, StringComparer.Ordinal);
        Assert.NotEqual(one, recipesReordered, StringComparer.Ordinal);
    }

    [Fact]
    public void A_moved_hash_or_a_gained_capability_moves_the_digest()
    {
        var before = ApproveAllDigest.Of([Row("workflow.step", 'a', "step.produce")]);

        Assert.NotEqual(before, ApproveAllDigest.Of([Row("workflow.step", 'c', "step.produce")]), StringComparer.Ordinal);
        Assert.NotEqual(before, ApproveAllDigest.Of([Row("workflow.step", 'a', "step.produce", "step.ship.open")]), StringComparer.Ordinal);
    }
}
