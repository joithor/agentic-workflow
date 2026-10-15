using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>
/// RCP-URG-001, RCP-OUT-001/002/003 and RCP-TYPE-001/002/003 (spec §3.2, §3.3, §9.1 "Added (UI E-21, E-22)"). The
/// recipes mirror invalid/RCP-URG-001.yaml and its siblings.
/// </summary>
public sealed class DeclarationRulesTests
{
    private static readonly (string, RecipeState)[] _produceThenDone = [("produce", Active("step.produce")), ("done", Terminal())];

    private static Recipe Base(IReadOnlyList<RecipeOutput>? outputs = null, IReadOnlyList<RecipeInput>? inputs = null, string urgency = "work") =>
        Of("produce", _produceThenDone, [Go("produce", "done", when: Outcome("done"))]) with
        {
            Urgency = urgency,
            Outputs = outputs ?? [],
            Inputs = inputs ?? [],
        };

    private static RecipeOutput Out(string key, string from, string? type = "file") => new(FactKey.From(key), Label: null, type, from);

    private static RecipeInput In(string key, string? type = null, string? kind = null) => new(FactKey.From(key), Label: null, type, kind);

    private static Located At(params (string Path, int Line, int Column)[] positions) => new("r.yaml", positions);

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_that_declares_nothing_has_no_findings()
    {
        Assert.Empty(CheckDeclarations(Base()));
        Assert.Empty(CheckDeclarations(Base(
            outputs: [Out("fact:output", "produce")],
            inputs: [In("fact:item", "item", "item"), In("fact:note", type: null, kind: "text"), In("fact:p", "payload", "webhook")],
            urgency: "incident")));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_urgency_other_than_incident_triage_or_work_is_reported()
    {
        Assert.Equal(
            ["RCP-URG-001: urgency 'urgent' isn't one of incident, triage, work at r.yaml:4:1 (urgency); use incident, triage or work, or remove urgency (the default is work)"],
            CheckDeclarations(Base(urgency: "urgent"), At(("urgency", 4, 1))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_from_a_state_the_recipe_doesnt_declare_is_reported()
    {
        Assert.Equal(
            ["RCP-OUT-001: output 'fact:output' comes from state 'draft', which the recipe doesn't declare at r.yaml:5:48 (outputs[0].from); name one of its states: produce, done"],
            CheckDeclarations(Base(outputs: [Out("fact:output", "draft")]), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_from_a_terminal_state_is_reported()
    {
        Assert.Equal(
            ["RCP-OUT-002: output 'fact:output' comes from terminal state 'done', which produces nothing at r.yaml:5:48 (outputs[0].from); name the state that produces it"],
            CheckDeclarations(Base(outputs: [Out("fact:output", "done")]), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("parked")]
    [InlineData("escalated")]
    [InlineData("taken-over")]
    [InlineData("cancelled")]
    public void An_output_from_an_engine_owned_state_is_reported_as_OUT_002_not_OUT_001(string state)
    {
        Assert.Equal(
            [$"RCP-OUT-002: output 'fact:output' comes from '{state}', a state the engine owns, not one of the recipe's at r.yaml:5:48 (outputs[0].from); name the recipe state that produces it"],
            CheckDeclarations(Base(outputs: [Out("fact:output", state)]), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_nothing_in_its_state_records_is_reported_with_what_the_state_does_record()
    {
        Assert.Equal(
            ["RCP-OUT-002: output 'fact:report' comes from state 'produce', but nothing there records it (step 'step.produce' records fact:output) at r.yaml:5:48 (outputs[0].from); have the state's Step, a transition leaving it ('produces') or its wait record 'fact:report', or name the state that does"],
            CheckDeclarations(Base(outputs: [Out("fact:report", "produce")]), At(("outputs[0].from", 5, 48))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_a_leaving_transition_produces_or_an_answer_wait_records_passes()
    {
        var recipe = Of(
            "produce",
            [("produce", Active("step.produce")), ("asked", WaitForAnswer("accept")), ("done", Terminal())],
            [
                Go("produce", "asked", when: Outcome("done"), produces: ["fact:report"]),
                Go("asked", "done", "wait.satisfied", Outcome("answered")),
            ]) with
        {
            Outputs = [Out("fact:report", "produce"), Out("fact:output", "produce"), Out("fact:answer", "asked", "decision")],
        };

        Assert.Empty(CheckDeclarations(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_from_a_state_waiting_on_a_child_is_information_when_nothing_records_it()
    {
        var recipe = Of(
            "running",
            [("running", WaitForChild("workflow.step")), ("done", Terminal())],
            [Go("running", "done", "wait.satisfied", AnyOf(Outcome("resolved"), Outcome("cancelled")))]) with
        {
            Outputs = [Out("fact:result", "running")],
        };

        var finding = Assert.Single(CheckDeclarations(recipe, At(("outputs[0].from", 5, 48))));

        Assert.Equal(FindingSeverity.Information, finding.Severity);
        Assert.Equal(
            "RCP-OUT-002: output 'fact:result' comes from state 'running', which waits on child 'workflow.step'; whether the child's outcome carries it can't be checked here at r.yaml:5:48 (outputs[0].from); list 'fact:result' in 'produces' on the transition leaving 'running' if the recipe should record it",
            finding.ToLine());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_duplicate_output_key_and_an_output_key_that_is_an_input_key_are_reported()
    {
        var recipe = Base(
            outputs: [Out("fact:output", "produce"), Out("fact:output", "produce"), Out("fact:item", "produce")],
            inputs: [In("fact:item")]);

        Assert.Equal(
            [
                "RCP-OUT-003: output key 'fact:output' is declared twice (first at outputs[0]) at r.yaml:6:10 (outputs[1].key); remove the duplicate, or give it its own key",
                "RCP-OUT-003: output key 'fact:item' is also an input key at r.yaml:7:10 (outputs[2].key); use another key for the output, since an input is already a fact the recipe starts with",
            ],
            CheckDeclarations(recipe, At(("outputs[1].key", 6, 10), ("outputs[2].key", 7, 10))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_type_that_isnt_a_fact_type_is_reported_on_inputs_and_outputs()
    {
        var recipe = Base(outputs: [Out("fact:output", "produce", "document")], inputs: [In("fact:item", "ticket")]);

        Assert.Equal(
            [
                "RCP-TYPE-001: input 'fact:item' has type 'ticket', which isn't one of text, file, item, link, scope-map, packet, evidence, decision, payload at r.yaml:4:30 (inputs[0].type); use one of them",
                "RCP-TYPE-001: output 'fact:output' has type 'document', which isn't one of text, file, item, link, scope-map, packet, evidence, decision, payload at r.yaml:6:30 (outputs[0].type); use one of them",
            ],
            CheckDeclarations(recipe, At(("inputs[0].type", 4, 30), ("outputs[0].type", 6, 30))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_kind_that_isnt_a_subject_kind_is_reported()
    {
        Assert.Equal(
            ["RCP-TYPE-002: input 'fact:item' has kind 'ticket', which isn't one of item, linear-project, webhook, file, text at r.yaml:4:40 (inputs[0].kind); use one of them"],
            CheckDeclarations(Base(inputs: [In("fact:item", "item", "ticket")]), At(("inputs[0].kind", 4, 40))).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("file", "item", "RCP-TYPE-003: input 'fact:item' has kind 'item' but type 'file'; kind item pairs with type item at r.yaml:4:40 (inputs[0].kind); set type to item, or change kind")]
    [InlineData(null, "item", "RCP-TYPE-003: input 'fact:item' has kind 'item' but type 'text' (the default); kind item pairs with type item at r.yaml:4:40 (inputs[0].kind); set type to item, or change kind")]
    [InlineData("text", "webhook", "RCP-TYPE-003: input 'fact:item' has kind 'webhook' but type 'text'; kind webhook pairs with type payload at r.yaml:4:40 (inputs[0].kind); set type to payload, or change kind")]
    public void A_kind_and_type_that_dont_pair_are_reported(string? type, string kind, string expected) =>
        Assert.Equal([expected], CheckDeclarations(Base(inputs: [In("fact:item", type, kind)]), At(("inputs[0].kind", 4, 40))).Lines());

    [Fact, Trait("Lane", "unit")]
    public void An_unpaired_check_is_skipped_for_an_entry_with_a_bad_type_or_kind()
    {
        var recipe = Base(inputs: [In("fact:a", "ticket", "item"), In("fact:b", "file", "ticket")]);

        Assert.True(CheckDeclarations(recipe).Select(f => f.Rule).SequenceEqual(["RCP-TYPE-001", "RCP-TYPE-002"], StringComparer.Ordinal));
    }
}
