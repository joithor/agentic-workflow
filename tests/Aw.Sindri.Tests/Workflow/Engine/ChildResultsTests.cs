using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.ChildFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>What a child's end hands its parent (DT spec §8.4, engine P7 plan D4): the declared outputs among its facts, on any outcome but 'cancelled'.</summary>
public sealed class ChildResultsTests
{
    private static readonly FactKey _output = FactKey.From("fact:output");

    private static Recipe Summary => RecipeComposition.ReadClean(ShippedRecipes.Read("dev/workflow.dev-child-summary.yaml"), "workflow.dev-child-summary.yaml").Recipe;

    private static Core.Workflow.Engine.Instances.WorkflowInstance Ended(string outcome, params (string Key, string Text)[] facts) =>
        Child(ChildId, "done", outcome: OutcomeName.From(outcome)) with { Facts = facts.ToDictionary(f => FactKey.From(f.Key), f => EngineFixture.Fact(f.Text)) };

    [Fact, Trait("Lane", "unit")]
    public void A_child_result_carries_its_declared_outputs_present_at_its_end()
    {
        var outputs = ChildResults.Outputs(Summary, Ended("resolved", ("fact:output", "summary.csv")));

        Assert.Equal(EngineFixture.Fact("summary.csv"), Assert.Single(outputs).Value);
        Assert.Equal(_output, Assert.Single(outputs).Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void Undeclared_child_facts_never_reach_the_parent()
    {
        var outputs = ChildResults.Outputs(Summary, Ended("resolved", ("fact:output", "summary.csv"), ("fact:findings", "notes"), ("fact:request", "do it")));

        Assert.Equal([_output], outputs.Keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancelled_child_carries_no_outputs() =>
        Assert.Empty(ChildResults.Outputs(Summary, Ended("cancelled", ("fact:output", "summary.csv"))));

    [Theory, Trait("Lane", "unit")]
    [InlineData("unresolved")]
    [InlineData("failed")]
    public void Any_other_outcome_carries_the_outputs_it_holds(string outcome) =>
        Assert.Equal([_output], ChildResults.Outputs(Summary, Ended(outcome, ("fact:output", "summary.csv"))).Keys);

    [Fact, Trait("Lane", "unit")]
    public void A_declared_output_the_child_never_produced_is_absent() =>
        Assert.Empty(ChildResults.Outputs(Summary, Ended("unresolved", ("fact:findings", "notes"))));
}
