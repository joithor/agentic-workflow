using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>Line spec App. B.1 'gates': no path through workflow.phase reaches 'merged' without prove, review and ship (DT10).</summary>
public sealed class GateDominanceTests
{
    [Fact, Trait("Lane", "unit")]
    public void No_path_through_phase_reaches_merged_without_prove_review_and_ship() =>
        Assert.Empty(GateDominance.Skipped(LineTree.Recipe("workflow.phase"), "merged", "prove", "review", "ship"));

    [Fact, Trait("Lane", "unit")]
    public void Ship_dominates_babysit_and_babysit_dominates_merged()
    {
        Assert.Empty(GateDominance.Skipped(LineTree.Recipe("workflow.phase"), "babysit", "ship"));
        Assert.Empty(GateDominance.Skipped(LineTree.Recipe("workflow.phase"), "merged", "babysit"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_walker_answers_per_state()
    {
        Assert.Empty(GateDominance.Skipped(LineTree.Recipe("workflow.phase"), "merged", "seed", "build"));
        Assert.Equal(["owner"], GateDominance.Skipped(LineTree.Recipe("workflow.phase"), "merged", "owner"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_phase_with_a_build_to_ship_edge_skips_prove_and_review()
    {
        var text = ShippedPhase().Replace(
            "transitions:\n",
            "transitions:\n  - { from: build, to: ship, on: wait.satisfied, when: { outcome: resolved } }\n",
            StringComparison.Ordinal);
        var mutated = RecipeComposition.ReadClean(text, "workflow.phase.yaml").Recipe;

        Assert.Equal(["prove", "review"], GateDominance.Skipped(mutated, "merged", "prove", "review", "ship"));
        Assert.Equal(["seed", "build", "ship", "babysit", "merged"], GateDominance.PathAround(mutated, "merged", "prove"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_walker_names_a_missing_state()
    {
        var phase = LineTree.Recipe("workflow.phase");

        _ = Assert.ThrowsAny<Exception>(() => GateDominance.Skipped(phase, "merged", "nope"));
    }

    private static string ShippedPhase() => ShippedRecipes.Read("sindri/workflow.phase.yaml");
}
