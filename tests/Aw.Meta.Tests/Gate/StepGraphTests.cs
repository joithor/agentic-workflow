using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class StepGraphTests
{
    private static StepConfig Build(string name, bool heavy = true) => new(name, ["dotnet", "build", name + ".slnx"], heavy);

    private static StepConfig Test(string name) => new(name, ["dotnet", "test", "--solution", "X.slnx", "--no-build"], Heavy: true);

    private static StepConfig Cheap(string name) => new(name, ["scripts/" + name + ".sh"]);

    private static string[] Names(IEnumerable<StepConfig> steps) => [.. steps.Select(s => s.Name)];

    private static StepGraph Graph(params StepConfig[] steps)
    {
        var graph = StepGraph.Build(steps, Names(steps));
        Assert.True(graph.IsSuccess, graph.IsError ? graph.Error.Message : string.Empty);
        return graph.Value;
    }

    private static string[] Waits(StepGraph graph, string name) =>
        [.. graph.DependenciesOf(Names(graph.Steps).ToList().IndexOf(name)).Select(i => graph.Steps[i].Name)];

    [Fact, Trait("Lane", "unit")]
    public void Dotnet_build_steps_chain_in_config_order()
    {
        var graph = Graph(Build("b1"), Build("b2"), Build("b3"));

        Assert.Empty(Waits(graph, "b1"));
        Assert.Equal(["b1"], Waits(graph, "b2"));
        Assert.Equal(["b2"], Waits(graph, "b3"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Heavy_and_dotnet_steps_wait_for_every_build_in_the_selection_and_cheap_checks_wait_for_nothing()
    {
        var graph = Graph(Cheap("sync"), Test("unit"), Build("build"), new StepConfig("bash-tests", ["bash", "scripts/ci/bash-tests.sh"], Heavy: true), new StepConfig("format", ["dotnet", "format", "--verify-no-changes"]));

        Assert.Empty(Waits(graph, "sync"));
        Assert.Empty(Waits(graph, "build"));
        Assert.Equal(["build"], Waits(graph, "unit"));
        Assert.Equal(["build"], Waits(graph, "bash-tests"));
        Assert.Equal(["build"], Waits(graph, "format"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_script_named_for_dotnet_waits_for_the_builds_unless_it_says_it_waits_for_nothing()
    {
        var restore = new StepConfig("verify-packages", ["scripts/dotnet-verify-packages.sh"]);
        var graph = Graph(Build("build"), restore, restore with { Name = "free", After = [] });

        Assert.Equal(["build"], Waits(graph, "verify-packages"));
        Assert.Empty(Waits(graph, "free"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_build_in_the_selection_nothing_waits()
    {
        var graph = Graph(Test("unit"), Cheap("sync"));

        Assert.Empty(Waits(graph, "unit"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dotnet_test_that_builds_itself_counts_as_a_build_and_chains()
    {
        var selfBuilding = new StepConfig("unit", ["dotnet", "test", "--solution", "X.slnx"], Heavy: true);
        var graph = Graph(Build("build"), selfBuilding, new StepConfig("integration", ["dotnet", "test", "--solution", "X.slnx"], Heavy: true));

        Assert.Equal(["build"], Waits(graph, "unit"));
        Assert.Equal(["unit"], Waits(graph, "integration"));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_explicit_after_list_replaces_the_inference_and_an_empty_one_waits_for_nothing()
    {
        var graph = Graph(Build("build"), Test("unit") with { After = [] }, Cheap("late") with { After = ["unit", "build"] });

        Assert.Empty(Waits(graph, "unit"));
        Assert.Equal(["unit", "build"], Waits(graph, "late"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_configured_step_that_was_not_selected_is_ignored_in_after()
    {
        var selected = new[] { Test("unit") with { After = ["build"] } };

        var graph = StepGraph.Build(selected, ["build", "unit"]);

        Assert.True(graph.IsSuccess);
        Assert.Empty(graph.Value.DependenciesOf(0));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_step_name_in_after_is_a_config_error_naming_it()
    {
        var graph = StepGraph.Build([Test("unit") with { After = ["biuld"] }], ["unit"]);

        Assert.True(graph.IsError);
        Assert.Contains("biuld", graph.Error.Message, StringComparison.Ordinal);
        Assert.Equal("AW-META-003", graph.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_and_a_step_waiting_for_itself_are_config_errors()
    {
        var a = Cheap("a") with { After = ["b"] };
        var b = Cheap("b") with { After = ["a"] };
        var self = Cheap("c") with { After = ["c"] };

        var cycle = StepGraph.Build([a, b], ["a", "b"]);
        var itself = StepGraph.Build([self], ["c"]);

        Assert.True(cycle.IsError);
        Assert.Contains("cycle", cycle.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(itself.IsError);
    }
}
