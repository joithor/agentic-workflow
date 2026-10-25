using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>
/// RCP-DISPLAY-001 (a recipe's required displayName), -002 (a state's optional why) and -003 (a state's optional displayName and a transition's optional label), setup-once spec §5.3.
/// The recipes mirror invalid/RCP-DISPLAY-001.yaml and its siblings.
/// </summary>
public sealed class DisplayRulesTests
{
    private static Recipe Base(string? displayName = "Produce and finish", string? why = null, string? stateName = null, string? label = null)
    {
        var recipe = Of("produce", [("produce", Active("step.produce") with { Why = why, DisplayName = stateName }), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done")) with { Label = label }]);
        return recipe with { DisplayName = displayName };
    }

    private static Located At(params (string Path, int Line, int Column)[] positions) => new("r.yaml", positions);

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_with_plain_words_everywhere_has_no_findings() =>
        Assert.Empty(CheckDisplay(Base("Idea to merged pull request", why: "Decides what will change before any code is written.", stateName: "Write the draft", label: "gaps found: draft again")));

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_without_a_display_name_is_RCP_DISPLAY_001()
    {
        Assert.Equal(
            ["RCP-DISPLAY-001: the recipe has no displayName at r.yaml:2:1 (displayName); add displayName: a name in sentence case, at most 60 characters, that says what the recipe does ('Idea to merged pull request')"],
            CheckDisplay(Base(displayName: null), At(("displayName", 2, 1))).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("build one phase", "it doesn't start with a capital letter (sentence case)")]
    [InlineData("Build one phase.", "it ends with a full stop (a name is not a sentence)")]
    [InlineData("workflow.build", "it doesn't start with a capital letter (sentence case)")]
    [InlineData("Run step.produce for the item", "'step.produce' is an id or code name, not plain words")]
    [InlineData("Build with BlockStep", "'BlockStep' is an id or code name, not plain words")]
    [InlineData("", "it is empty")]
    [InlineData("A name that goes on and on and on and on, far past sixty characters", "it is 67 characters, over the limit of 60")]
    public void A_display_name_that_is_not_plain_words_is_RCP_DISPLAY_001(string name, string problem)
    {
        var line = Assert.Single(CheckDisplay(Base(name), At(("displayName", 2, 1))).Lines());

        Assert.StartsWith($"RCP-DISPLAY-001: displayName '{name}': {problem} at r.yaml:2:1 (displayName); ", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_display_name_of_exactly_sixty_characters_passes()
    {
        var name = "A" + new string('b', 59);

        Assert.Empty(CheckDisplay(Base(name)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Runs step.produce, then step.checks.", "'step.produce' is an id or code name, not plain words")]
    [InlineData("See RCP-URG-001 for the reason.", "'RCP-URG-001' is an id or code name, not plain words")]
    [InlineData("Reads fact:output from the last step.", "'fact:output' is an id or code name, not plain words")]
    [InlineData("Checks the `sha256` of the packet.", "'`' is an id or code name, not plain words")]
    public void A_why_with_an_id_or_code_name_is_RCP_DISPLAY_002(string why, string problem)
    {
        var line = Assert.Single(CheckDisplay(Base(why: why), At(("states.produce.why", 5, 7))).Lines());

        Assert.Equal(
            $"RCP-DISPLAY-002: the why of state 'produce': {problem} at r.yaml:5:7 (states.produce.why); write one plain sentence of at most 200 characters, with no id or code name",
            line);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_why_over_200_characters_is_RCP_DISPLAY_002_and_200_passes()
    {
        Assert.Empty(CheckDisplay(Base(why: new string('x', 200))));

        var line = Assert.Single(CheckDisplay(Base(why: new string('x', 201))).Lines());

        Assert.Contains("it is 201 characters, over the limit of 200", line, StringComparison.Ordinal);
        Assert.StartsWith("RCP-DISPLAY-002: ", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_display_name_that_is_not_plain_words_is_RCP_DISPLAY_003()
    {
        Assert.Equal(
            ["RCP-DISPLAY-003: the displayName of state 'produce': it doesn't start with a capital letter (sentence case) at r.yaml:5:7 (states.produce.displayName); write a name in sentence case, at most 60 characters, with no id"],
            CheckDisplay(Base(stateName: "write the draft"), At(("states.produce.displayName", 5, 7))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_label_with_an_id_or_over_sixty_characters_is_RCP_DISPLAY_003()
    {
        Assert.Equal(
            ["RCP-DISPLAY-003: the label of the transition to 'done': 'step.verify' is an id or code name, not plain words at r.yaml:9:5 (transitions[0].label); write plain words of at most 60 characters ('gaps found: draft again'), with no id or code name"],
            CheckDisplay(Base(label: "step.verify accepted"), At(("transitions[0].label", 9, 5))).Lines());
        Assert.Contains("over the limit of 60", Assert.Single(CheckDisplay(Base(label: new string('x', 61))).Lines()), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_label_may_start_in_lower_case_and_a_why_may_end_in_a_full_stop() =>
        Assert.Empty(CheckDisplay(Base(label: "gaps found: draft again", why: "Ends in a full stop.")));

    private static Recipe WithOnOutcome(string? why = null, params (string Outcome, string Text)[] texts)
    {
        var recipe = Of("produce", [("produce", Active("step.produce") with { OnOutcome = texts.ToDictionary(static t => t.Outcome, static t => t.Text, StringComparer.Ordinal) }), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"))]);
        return recipe with { DisplayName = "Produce and finish", Why = why };
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_why_that_is_not_plain_words_is_RCP_DISPLAY_002()
    {
        Assert.Empty(CheckDisplay(WithOnOutcome(why: "Gets the work from idea to merged pull request.")));
        Assert.Equal(
            ["RCP-DISPLAY-002: the why of the recipe: 'step.produce' is an id or code name, not plain words at r.yaml:4:1 (why); write one plain sentence of at most 200 characters, with no id or code name"],
            CheckDisplay(WithOnOutcome(why: "Runs step.produce once."), At(("why", 4, 1))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_on_outcome_text_keyed_by_an_outcome_of_the_step_is_accepted() =>
        Assert.Empty(CheckDisplay(WithOnOutcome(why: null, texts: [("done", "Moves on to the next stage.")])));

    [Fact, Trait("Lane", "unit")]
    public void An_on_outcome_key_the_step_does_not_report_is_RCP_DISPLAY_002()
    {
        Assert.Equal(
            ["RCP-DISPLAY-002: the onOutcome of state 'produce' names 'approve', which its Step doesn't report (done) at r.yaml:6:9 (states.produce.onOutcome.approve); key each onOutcome text by an outcome of the state's Step"],
            CheckDisplay(WithOnOutcome(why: null, texts: [("approve", "Moves on.")]), At(("states.produce.onOutcome.approve", 6, 9))).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_on_outcome_text_that_is_not_plain_words_is_RCP_DISPLAY_002()
    {
        var line = Assert.Single(CheckDisplay(WithOnOutcome(why: null, texts: [("done", "Writes fact:output.")])).Lines());

        Assert.StartsWith("RCP-DISPLAY-002: the onOutcome text for 'done' of state 'produce': 'fact:output' is an id or code name, not plain words", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_on_outcome_on_a_state_that_runs_no_Step_is_RCP_DISPLAY_002()
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal) { ["yes"] = "Goes on." };
        var recipe = Of("wait", [("wait", WaitForAnswer("go") with { OnOutcome = texts }), ("done", Terminal())], [Go("wait", "done", on: "wait.satisfied")]);
        recipe = recipe with { DisplayName = "Wait and finish" };

        var line = Assert.Single(CheckDisplay(recipe).Lines());

        Assert.StartsWith("RCP-DISPLAY-002: state 'wait' has onOutcome but runs no Step", line, StringComparison.Ordinal);
    }
}
