namespace Aw.Sindri.Tests.Workflow.Choice;

/// <summary>RCP-CHOICE-001 to -006 (DT spec §3.2, §8.1): each rule's message and fix, from the invalid fixtures and small edits of the compact route.</summary>
public sealed class ChoiceRulesTests
{
    private const string Choices = "          - \"ui: what a user sees changed\"\n          - \"api: a contract changed\"\n";

    [Fact, Trait("Lane", "unit")]
    public void The_compact_route_is_valid() => Assert.Empty(RouteRecipes.Lines(RouteRecipes.Compact));

    [Fact, Trait("Lane", "unit")]
    public void A_choice_list_of_one_is_RCP_CHOICE_001() =>
        Only("RCP-CHOICE-001", RouteRecipes.Fixture("RCP-CHOICE-001"), "RCP-CHOICE-001: state 'route' gives 'step.judge.choose' 1 choices", "list 2 to 32 choices, each \"<outcome>: <criterion>\"");

    [Fact, Trait("Lane", "unit")]
    public void More_than_32_choices_is_RCP_CHOICE_001()
    {
        var many = string.Concat(Enumerable.Range(0, 33).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"          - \"c{i}: criterion {i}\"\n")));

        Only("RCP-CHOICE-001", RouteRecipes.Edit(RouteRecipes.Compact, Choices, many).Replace("outcome: ui", "outcome: c0", StringComparison.Ordinal).Replace("outcome: api", "outcome: c1", StringComparison.Ordinal),
            "RCP-CHOICE-001: state 'route' gives 'step.judge.choose' 33 choices", "list 2 to 32 choices, each \"<outcome>: <criterion>\"", allowMore: true);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unquoted_choice_is_RCP_CHOICE_002_with_the_quote_fix() =>
        Only("RCP-CHOICE-002", RouteRecipes.Fixture("RCP-CHOICE-002-mapping"), "RCP-CHOICE-002: entry 1 of choices in state 'route' is a YAML mapping", "quote it: \"<key>: <value>\"");

    [Fact, Trait("Lane", "unit")]
    public void A_fact_valued_choices_list_is_RCP_CHOICE_002() =>
        Only("RCP-CHOICE-002", RouteRecipes.Edit(RouteRecipes.Compact, Choices, "").Replace("        choices:\n", "        choices: { fact: fact:change }\n", StringComparison.Ordinal),
            "RCP-CHOICE-002: choices of state 'route' is a { fact } value", "write the choices as a literal list, so approving the recipe fixes its branches");

    [Fact, Trait("Lane", "unit")]
    public void A_choices_value_that_is_not_a_list_is_RCP_CHOICE_002() =>
        Only("RCP-CHOICE-002", RouteRecipes.Edit(RouteRecipes.Compact, Choices, "").Replace("        choices:\n", "        choices: nothing\n", StringComparison.Ordinal),
            "RCP-CHOICE-002: choices of state 'route' isn't a list", "write the choices as a literal list, so approving the recipe fixes its branches", allowMore: true);

    [Fact, Trait("Lane", "unit")]
    public void A_choice_that_is_a_number_is_RCP_CHOICE_002() =>
        Only("RCP-CHOICE-002", RouteRecipes.Edit(RouteRecipes.Compact, "          - \"ui: what a user sees changed\"\n", "          - 5\n"),
            "RCP-CHOICE-002: entry 1 of choices in state 'route' is a number, not text", "write it as \"<outcome>: <criterion>\"");

    [Fact, Trait("Lane", "unit")]
    public void A_choice_without_a_separator_is_RCP_CHOICE_002() =>
        Only("RCP-CHOICE-002", RouteRecipes.Fixture("RCP-CHOICE-002"), "RCP-CHOICE-002: entry 1 of choices in state 'route' has no ': ' after its outcome", "write it as \"<outcome>: <criterion>\"");

    [Fact, Trait("Lane", "unit")]
    public void A_choice_with_a_bad_outcome_name_is_RCP_CHOICE_002() =>
        Only("RCP-CHOICE-002", RouteRecipes.Edit(RouteRecipes.Compact, "\"ui: what a user", "\"Ui: what a user"),
            "RCP-CHOICE-002: entry 1 of choices in state 'route' names outcome 'Ui', which isn't [a-z][A-Za-z0-9]{0,31}", "start with a-z and use only letters and digits, at most 32 characters");

    [Fact, Trait("Lane", "unit")]
    public void A_camelCase_choice_name_is_accepted_and_routes() =>
        Assert.Empty(RouteRecipes.Lines(RouteRecipes.Edit(RouteRecipes.Edit(RouteRecipes.Compact, "\"ui: what a user", "\"notStartable: what a user"), "outcome: ui", "outcome: notStartable")));

    [Fact, Trait("Lane", "unit")]
    public void A_hyphenated_choice_name_is_RCP_CHOICE_002() =>
        Only("RCP-CHOICE-002", RouteRecipes.Edit(RouteRecipes.Compact, "\"ui: what a user", "\"not-startable: what a user"),
            "RCP-CHOICE-002: entry 1 of choices in state 'route' names outcome 'not-startable', which isn't [a-z][A-Za-z0-9]{0,31}", "start with a-z and use only letters and digits, at most 32 characters");

    [Fact, Trait("Lane", "unit")]
    public void A_choice_with_an_empty_criterion_is_RCP_CHOICE_002() =>
        Only("RCP-CHOICE-002", RouteRecipes.Edit(RouteRecipes.Compact, "\"ui: what a user sees changed\"", "\"ui: \""),
            "RCP-CHOICE-002: entry 1 of choices in state 'route' has a criterion of 0 characters", "write a criterion of 1 to 400 characters");

    [Fact, Trait("Lane", "unit")]
    public void A_choice_with_a_criterion_over_400_characters_is_RCP_CHOICE_002() =>
        Only("RCP-CHOICE-002", RouteRecipes.Edit(RouteRecipes.Compact, "what a user sees changed", new string('x', 401)),
            "RCP-CHOICE-002: entry 1 of choices in state 'route' has a criterion of 401 characters", "write a criterion of 1 to 400 characters");

    [Fact, Trait("Lane", "unit")]
    public void Two_choices_with_one_outcome_are_RCP_CHOICE_003() =>
        Only("RCP-CHOICE-003", RouteRecipes.Fixture("RCP-CHOICE-003"), "RCP-CHOICE-003: entries 1 and 2 of choices in state 'route' both name outcome 'api'", "give each choice its own outcome");

    [Fact, Trait("Lane", "unit")]
    public void A_choice_named_uncertain_is_RCP_CHOICE_003() =>
        Only("RCP-CHOICE-003", RouteRecipes.Edit(RouteRecipes.Compact, "\"api: a contract", "\"uncertain: a contract"),
            "RCP-CHOICE-003: entry 2 of choices in state 'route' names outcome 'uncertain', which 'step.judge.choose' adds itself", "rename the choice; route uncertain with its own transition", allowMore: true);

    [Fact, Trait("Lane", "unit")]
    public void An_empty_facts_list_is_RCP_CHOICE_004() =>
        Only("RCP-CHOICE-004", RouteRecipes.Fixture("RCP-CHOICE-004"), "RCP-CHOICE-004: state 'route' gives 'step.judge.choose' no facts to read", "list at least one fact key under facts");

    [Fact, Trait("Lane", "unit")]
    public void A_profile_valued_facts_list_is_RCP_CHOICE_004() =>
        Only("RCP-CHOICE-004", RouteRecipes.Edit(RouteRecipes.Compact, "facts: [fact:change]", "facts: { profile: workflow.facts }"),
            "RCP-CHOICE-004: facts of state 'route' is a { profile } value", "write facts as a literal list of fact keys");

    [Fact, Trait("Lane", "unit")]
    public void A_facts_value_that_is_not_a_list_is_RCP_CHOICE_004() =>
        Only("RCP-CHOICE-004", RouteRecipes.Edit(RouteRecipes.Compact, "facts: [fact:change]", "facts: nothing"),
            "RCP-CHOICE-004: facts of state 'route' isn't a list", "write facts as a literal list of fact keys", allowMore: true);

    [Fact, Trait("Lane", "unit")]
    public void A_hyphenated_fact_in_facts_is_RCP_CHOICE_004_with_a_camelCase_fix() =>
        Only("RCP-CHOICE-004", RouteRecipes.Edit(RouteRecipes.Compact, "facts: [fact:change]", "facts: [\"fact:evidence-summary\"]"),
            "RCP-CHOICE-004: entry 1 of facts in state 'route', 'fact:evidence-summary', isn't a fact key", "fact names are camelCase: write fact:evidenceSummary");

    [Fact, Trait("Lane", "unit")]
    public void Uncertain_routed_off_the_desk_twin_is_RCP_CHOICE_005() =>
        Only("RCP-CHOICE-005", RouteRecipes.Fixture("RCP-CHOICE-005"), "RCP-CHOICE-005: state 'route' routes uncertain to 'prove', which isn't its desk twin",
            "route it to a state with step: { use: step.desk.choose, with: { from: route } }");

    [Fact, Trait("Lane", "unit")]
    public void A_route_without_step_failed_is_RCP_CHOICE_005() =>
        Only("RCP-CHOICE-005", RouteRecipes.Edit(RouteRecipes.Compact, "  - { from: route, to: route-desk, on: step.failed }\n", string.Empty),
            "RCP-CHOICE-005: state 'route' has no step.failed transition to its desk twin", "add { from: route, to: route-desk, on: step.failed }", allowMore: true);

    [Fact, Trait("Lane", "unit")]
    public void A_route_with_no_desk_twin_at_all_says_to_add_one()
    {
        var noTwin = RouteRecipes.Edit(RouteRecipes.Compact, "  route-desk:\n    kind: active\n    step: { use: step.desk.choose, with: { from: route } }\n    timeout: { after: 2d, then: park }\n    next: \"Pick the evidence.\"\n", string.Empty);

        var lines = RouteRecipes.Lines(noTwin);

        Assert.Contains(lines, l => l.StartsWith("RCP-CHOICE-005: state 'route' has no step.failed transition to its desk twin at ", StringComparison.Ordinal)
            && l.EndsWith("; add { from: route, to: <its desk twin>, on: step.failed }", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_desk_twin_from_a_non_route_state_is_RCP_CHOICE_005() =>
        Only("RCP-CHOICE-005", RouteRecipes.Edit(RouteRecipes.Compact, "with: { from: route }", "with: { from: prove }"),
            "RCP-CHOICE-005: state 'route-desk' runs 'step.desk.choose' from 'prove', which isn't a 'step.judge.choose' state", "set from: to the route state it twins", allowMore: true);

    [Theory, Trait("Lane", "unit")]
    [InlineData("{ profile: workflow.routeState }", "{ profile }")]
    [InlineData("{ fact: fact:change }", "{ fact }")]
    public void A_desk_twin_whose_from_is_not_a_literal_state_is_RCP_CHOICE_005(string from, string called) =>
        Only("RCP-CHOICE-005", RouteRecipes.Edit(RouteRecipes.Compact, "with: { from: route }", $"with: {{ from: {from} }}"),
            $"RCP-CHOICE-005: from of state 'route-desk' is a {called} value", "name the route state it twins as a literal: from: <state>");

    [Fact, Trait("Lane", "unit")]
    public void Step_failed_before_a_branch_is_RCP_CHOICE_006() =>
        Only("RCP-CHOICE-006", RouteRecipes.Fixture("RCP-CHOICE-006"), "RCP-CHOICE-006: transitions[0] routes 'route' on step.failed before its branch transitions[1]",
            "list the branch transitions first and the uncertain and step.failed ones last");

    [Fact, Trait("Lane", "unit")]
    public void Uncertain_before_a_branch_is_RCP_CHOICE_006()
    {
        var moved = RouteRecipes.Edit(RouteRecipes.Compact, "  - { from: route, to: route-desk, on: step.completed, when: { outcome: uncertain }, produces: [fact:decision] }\n", string.Empty);
        moved = RouteRecipes.Edit(moved, "transitions:\n", "transitions:\n  - { from: route, to: route-desk, on: step.completed, when: { outcome: uncertain }, produces: [fact:decision] }\n");

        Only("RCP-CHOICE-006", moved, "RCP-CHOICE-006: transitions[0] routes 'route' on uncertain before its branch transitions[1]", "list the branch transitions first and the uncertain and step.failed ones last");
    }

    /// <summary>The recipe's findings hold exactly one line of <paramref name="rule"/> (unless <paramref name="allowMore"/>), reading <paramref name="message"/> and ending in <paramref name="fix"/>.</summary>
    private static void Only(string rule, string yaml, string message, string fix, bool allowMore = false)
    {
        var lines = RouteRecipes.Lines(yaml);

        var own = Assert.Single(lines, l => l.StartsWith(rule + ": ", StringComparison.Ordinal) && l.StartsWith(message + " at ", StringComparison.Ordinal));
        Assert.EndsWith("; " + fix, own, StringComparison.Ordinal);
        if (!allowMore)
        {
            Assert.Equal([own], lines);
        }
    }
}
