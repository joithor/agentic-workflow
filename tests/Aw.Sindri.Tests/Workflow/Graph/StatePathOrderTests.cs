using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>Path order of states (spec §3.10, UI E-20): the first transition to an unvisited state is the main path, later ones branch, back edges are loops, terminals come last.</summary>
public sealed class StatePathOrderTests
{
    private static string Places(PathOrder order) =>
        string.Join(' ', order.States.Select(s => $"{s.State}={s.Order?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}{(s.BranchOf is null ? string.Empty : $"^{s.BranchOf}")}"));

    private static PathOrder Of(string initial, (string Name, RecipeState State)[] states, params RecipeTransition[] transitions) =>
        StatePathOrder.Of(GraphRecipe.Of(initial, states, transitions));

    private static PathOrder Shipped(string path) =>
        StatePathOrder.Of(RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path)).Recipe);

    [Fact, Trait("Lane", "unit")]
    public void A_straight_recipe_is_numbered_in_walk_order_with_the_terminal_last()
    {
        var order = Of("a", [("a", Active()), ("b", Active()), ("done", Terminal())], Go("a", "b"), Go("b", "done"));

        Assert.Equal("a=1 b=2 done=3", Places(order));
        Assert.Equal("path", order.OrderedBy);
        Assert.Empty(order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_listed_transition_to_an_unvisited_state_is_the_main_path_and_a_later_one_is_a_branch()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("c", Active()), ("done", Terminal())],
            Go("a", "b"), Go("a", "c"), Go("b", "done"), Go("c", "done"));

        Assert.Equal("a=1 b=2 c=-^a done=3", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_reached_by_both_a_main_path_transition_and_a_branch_keeps_its_main_path_place()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("c", Active()), ("done", Terminal())],
            Go("a", "b"), Go("a", "c"), Go("b", "c"), Go("c", "done"));

        Assert.Equal("a=1 b=2 c=3 done=4", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void What_only_a_branch_reaches_is_folded_under_the_state_the_branch_leaves()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("c", Active()), ("d", Active()), ("done", Terminal())],
            Go("a", "b"), Go("a", "c"), Go("c", "d"), Go("b", "done"), Go("d", "done"));

        Assert.Equal("a=1 b=2 c=-^a d=-^a done=3", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void Terminal_states_come_last_in_the_order_the_walk_found_them()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("failed", Terminal("failed")), ("done", Terminal())],
            Go("a", "b"), Go("a", "failed"), Go("b", "done"));

        Assert.Equal("a=1 b=2 done=3 failed=4", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_back_to_a_state_on_the_walk_is_a_loop_edge_not_a_path_step()
    {
        var order = Of(
            "a",
            [("a", Active(loop: "rounds")), ("b", Active()), ("done", Terminal())],
            Go("a", "b"), Go("b", "done"), Go("b", "a"));

        Assert.Equal("a=1 b=2 done=3", Places(order));
        Assert.Equal([new PathLoop("rounds", "b", "a")], order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loop_edge_is_named_by_the_looping_state_that_declares_a_loop_and_unnamed_when_none_does()
    {
        var named = Of("a", [("a", Active()), ("b", Active(loop: "fix")), ("done", Terminal())], Go("a", "b"), Go("b", "a"), Go("b", "done"));
        var unnamed = Of("a", [("a", Active()), ("b", Active()), ("done", Terminal())], Go("a", "b"), Go("b", "a"), Go("b", "done"));

        Assert.Equal([new PathLoop("fix", "b", "a")], named.Loops);
        Assert.Equal([new PathLoop(Name: null, "b", "a")], unnamed.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_that_loops_to_itself_is_one_loop_edge()
    {
        var order = Of("a", [("a", Active(loop: "retry")), ("done", Terminal())], Go("a", "a"), Go("a", "done"));

        Assert.Equal("a=1 done=2", Places(order));
        Assert.Equal([new PathLoop("retry", "a", "a")], order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_listed_for_several_from_states_counts_for_each()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("done", Terminal())],
            Go("a", "b"), new RecipeTransition(["a", "b"], "done", "step.failed", When: null, [], [], [], []));

        Assert.Equal("a=1 b=2 done=3", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void Several_distinct_first_targets_from_initial_say_the_order_follows_the_transition_list()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("c", Active()), ("done", Terminal())],
            Go("a", "b"), Go("a", "c"), Go("b", "done"), Go("c", "done"));
        var single = Of("a", [("a", Active()), ("b", Active()), ("done", Terminal())], Go("a", "b"), Go("a", "b", "step.failed"), Go("b", "done"));

        Assert.Equal("transition-list", order.OrderedBy);
        Assert.Equal("path", single.OrderedBy);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_the_walk_never_reaches_is_listed_last_with_neither_an_order_nor_a_branch()
    {
        var order = Of("a", [("a", Active()), ("orphan", Active()), ("done", Terminal())], Go("a", "done"));

        Assert.Equal("a=1 done=2 orphan=-", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_to_an_undeclared_state_is_not_followed()
    {
        var order = Of("a", [("a", Active()), ("done", Terminal())], Go("a", "nowhere"), Go("a", "done"));

        Assert.Equal("a=1 done=2", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_three_state_triage_recipe_is_desk_answer_done()
    {
        var order = Shipped("dev/triage-three-state.yaml");

        Assert.Equal("desk=1 answer=2 done=3", Places(order));
        Assert.Empty(order.Loops);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_desk_round_trip_recipe_is_export_import_done_with_the_loop_between_import_and_export()
    {
        var order = Shipped("dev/desk-round-trip.yaml");

        Assert.Equal("export=1 import=2 done=3", Places(order));
        Assert.Equal([new PathLoop("rounds", "import", "export")], order.Loops);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Every_state_of_a_shipped_recipe_has_an_order_or_a_branch(string path)
    {
        var order = Shipped(path);

        Assert.All(order.States, s => Assert.True(s.Order is not null || s.BranchOf is not null, $"{path}: {s.State}"));
    }


    [Theory, Trait("Lane", "unit")]
    [InlineData("sindri/workflow.item.yaml", "sha256:496cdcbfe884c70dce832d96d21883660f29fffe9b122fcfa72a04c98be8cae3")]
    [InlineData("sindri/workflow.step.yaml", "sha256:1b13bd920351e56227141fe24e59f2f90cb46bc3549e1e90f2da2af1c3be9f5c")]
    [InlineData("dev/desk-round-trip.yaml", "sha256:42e9e8ab897f90e38c74f9a3a5aa499f9a56475b4c1a416c0ad5b01a79bb2346")]
    [InlineData("dev/incident-hold.yaml", "sha256:6913fd55ce71cfcdf6dc8892ad1e5968480506af640cf065b4fece88307af6b4")]
    [InlineData("dev/triage-three-state.yaml", "sha256:88a0e8fc810fb8dbdccb244b1a601e16743ade3c4230b57c7c0cf782b3ed0f66")]
    [InlineData("dev/workflow.dev-notify.yaml", "sha256:a37120f25686700c1c0ad665eed881b851bfdcd55f7605fcd5aef46fb3988e87")]
    [InlineData("dev/workflow.dev-timeout.yaml", "sha256:8156a3b02f9e0d824cf56be5ade5f0b7be81c0ff4a9a5a49c882d68132f3a043")]
    public void The_path_order_is_not_hashed_so_every_shipped_recipe_keeps_its_hash(string path, string hash)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));
        var json = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe, SliceHarness.Json));

        Assert.Equal(hash, document.Hash.Value);
        Assert.DoesNotContain("orderedBy", json, StringComparison.Ordinal);
        Assert.DoesNotContain("branchOf", json, StringComparison.Ordinal);
    }

    public static TheoryData<string> Recipes => [.. ShippedRecipes.Recipes];
}
