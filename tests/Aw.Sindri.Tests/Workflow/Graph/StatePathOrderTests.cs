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
    public void A_loop_edge_is_named_by_the_target_loop_else_the_looping_states_and_unnamed_when_none_declares_one()
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
        Assert.Equal("path", order.OrderedBy);
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
        Assert.Equal("path", order.OrderedBy);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_terminal_target_listed_first_neither_makes_the_order_follow_the_list_nor_takes_the_main_path()
    {
        var order = Of(
            "a",
            [("a", Active()), ("b", Active()), ("failed", Terminal("failed")), ("done", Terminal())],
            Go("a", "failed"), Go("a", "b"), Go("b", "done"));

        Assert.Equal("path", order.OrderedBy);
        Assert.Equal("a=1 b=2 failed=3 done=4", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_shipped_item_recipe_is_ordered_by_path()
    {
        var order = Shipped("sindri/workflow.item.yaml");

        Assert.Equal("path", order.OrderedBy);
        Assert.Equal("triaged=1 startable=2 claimed=3 running=4 done=5 dropped=6", Places(order));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_nested_loops_back_edge_is_named_by_the_target_loop_the_engine_counts_the_round_on()
    {
        var order = Of(
            "a",
            [("a", Active(loop: "outer")), ("b", Active(loop: "inner")), ("done", Terminal())],
            Go("a", "b"), Go("b", "a"), Go("b", "done"));

        Assert.Equal([new PathLoop("outer", "b", "a")], order.Loops);
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
    [InlineData("sindri/workflow.item.yaml", "sha256:cb9a5b5e28c3dbfaa457ea21304c977b04e3d871cdbeca32252825b9a8c9d4fb")]
    [InlineData("sindri/workflow.step.yaml", "sha256:c3b7814f56b48b918e8f399481b44454627a6a2b10a9c26dc930826b1586cc2e")]
    [InlineData("dev/desk-round-trip.yaml", "sha256:90d97fc7ac4c79284d743986abd59a03bf67bc54c4abb8581d711496669c695f")]
    [InlineData("dev/incident-hold.yaml", "sha256:bc5999c62f5e4ae59b3104eacdc049f76756b619bf7425b1171966e9d543a4fe")]
    [InlineData("dev/triage-three-state.yaml", "sha256:3618e0f863383d3018199adb7bd5433494e7caaeaf3df72fe086354913087236")]
    [InlineData("dev/workflow.dev-notify.yaml", "sha256:09a2b31dec3b0865cf2f108c179d01fc4b7e1d7e5889340903d5c96d9439de74")]
    [InlineData("dev/workflow.dev-timeout.yaml", "sha256:63b2e0ece7db196e311fa6cf54265f3d227b8d8855bd28a7b3150f282b2737f3")]
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
