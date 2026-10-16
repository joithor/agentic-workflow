using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>RCP-WIRE-003 (DT-C, DT spec §8.4): a parent produces a child's output only on the outcomes whose every path in the child produces it.</summary>
public sealed class ChildOutputRulesTests
{
    /// <summary>workflow.kid declares fact:output, produced on the way to resolved and not to unresolved.</summary>
    private static readonly InMemoryChildRecipes _kid = new InMemoryChildRecipes().With(Child(
        Of(
            "produce",
            [("produce", Active("step.produce")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]), Go("produce", "dropped", "command.skip")],
            inputs: [],
            id: "workflow.kid") with
        {
            Outputs = [OutputOf("fact:output", "produce")],
        }));

    /// <summary>A parent waiting on workflow.kid whose transition (transitions[0]) is taken on <paramref name="when"/>.</summary>
    private static Recipe Parent(WhenClause? when, params ProducedFact[] produces) =>
        Of(
            "running",
            [("running", WaitForChild("workflow.kid")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [GoProducing("running", "done", "wait.satisfied", when, produces)],
            inputs: []);

    private static Located WithKid => Located.Nowhere with { Children = _kid };

    private static IReadOnlyList<string> Wire003(IEnumerable<string> lines) => [.. lines.Where(l => l.StartsWith("RCP-WIRE-003", StringComparison.Ordinal))];

    [Fact, Trait("Lane", "unit")]
    public void Producing_an_output_missing_on_a_path_to_the_outcome_is_RCP_WIRE_003() =>
        Assert.Equal(
            ["RCP-WIRE-003: transition from 'running' produces 'fact:output' on outcome 'unresolved' of child 'workflow.kid', which doesn't produce 'fact:output' on every path to 'unresolved' (in workflow.kid, the path through transitions[1] from 'produce' doesn't produce it) at r.yaml:1:1 (transitions[0].produces[0]); produce it on that path in workflow.kid, or produce it here only on outcomes that always carry it (resolved)"],
            Wire003(CheckWiring(Parent(Outcome("unresolved"), Bare("fact:output")), WithKid).Lines()));

    [Fact, Trait("Lane", "unit")]
    public void Producing_on_cancelled_is_RCP_WIRE_003() =>
        Assert.Equal(
            ["RCP-WIRE-003: transition from 'running' produces 'fact:output' on outcome 'cancelled' of child 'workflow.kid', which carries no outputs at r.yaml:1:1 (transitions[0].produces[0]); route cancelled with a transition of its own that produces nothing"],
            Wire003(CheckWiring(Parent(Outcome("cancelled"), Bare("fact:output")), WithKid).Lines()));

    [Fact, Trait("Lane", "unit")]
    public void Producing_on_any_outcome_is_RCP_WIRE_003() =>
        Assert.Equal(
            ["RCP-WIRE-003: transition from 'running' produces 'fact:output' from child 'workflow.kid' on any outcome, 'cancelled' among them at r.yaml:1:1 (transitions[0].produces[0]); add when: { outcome: <o> } naming outcomes that always carry it (resolved)"],
            Wire003(CheckWiring(Parent(when: null, Bare("fact:output")), WithKid).Lines()));

    [Fact, Trait("Lane", "unit")]
    public void An_all_clause_naming_a_carrying_outcome_is_accepted()
    {
        var present = new WhenClause(Outcome: null, FactKey.From("fact:output"), FactPresence.Present, Loop: null, Below: null, All: null, Any: null, Not: null);

        Assert.Empty(Wire003(CheckWiring(Parent(AllOf(Outcome("resolved"), present), Bare("fact:output")), WithKid).Lines()));
        Assert.Empty(Wire003(CheckWiring(Parent(Outcome("resolved"), Rename("fact:exported", "fact:output")), WithKid).Lines()));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_undeclared_output_gives_only_RCP_WIRE_002()
    {
        var lines = CheckWiring(Parent(Outcome("unresolved"), Bare("fact:findings")), WithKid).Lines();

        Assert.Empty(Wire003(lines));
        _ = Assert.Single(lines, l => l.StartsWith("RCP-WIRE-002", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unresolved_child_gives_only_RCP_CHILD_001() =>
        Assert.All(CheckWiring(Parent(Outcome("resolved"), Bare("fact:output"))).Lines(), l => Assert.StartsWith("RCP-CHILD-001", l, StringComparison.Ordinal));
}
