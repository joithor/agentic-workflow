using System.Text.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The YAML recipe reader (spec §3.1): core-schema scalars, every node's position, every finding, and normalization of each short form.</summary>
public sealed class RecipeReaderTests
{
    internal static readonly string Minimal =
        "schema: 1\n"
        + "id: workflow.minimal\n"
        + "version: 1\n"
        + "initial: produce\n"
        + "states:\n"
        + "  produce:\n"
        + "    kind: active\n"
        + "    step: step.produce\n"
        + "    timeout: { after: 4h, then: park }\n"
        + "    next: \"Produce the Step output.\"\n"
        + "  done:\n"
        + "    kind: terminal\n"
        + "    outcome: resolved\n"
        + "transitions:\n"
        + "  - { from: produce, to: done, on: step.completed, when: { outcome: done }, produces: [fact:output] }\n";

    internal static IRecipeReader Reader { get; } = new YamlRecipeReader(SliceHarness.Json);

    internal static Recipe Bind(string yaml)
    {
        var reading = Reader.Read(yaml, "test.yaml");
        Assert.Empty(reading.Findings.Select(f => f.ToLine()));
        return reading.Document!.Recipe;
    }

    internal static IReadOnlyList<string> Lines(string yaml) => [.. Reader.Read(yaml, "test.yaml").Findings.Select(f => f.ToLine())];

    [Fact, Trait("Lane", "unit")]
    public void A_minimal_recipe_binds_with_no_findings()
    {
        var reading = Reader.Read(Minimal, "test.yaml");

        Assert.Empty(reading.Findings);
        Assert.Equal("workflow.minimal", reading.Document!.Recipe.Id.Value);
        Assert.Equal("test.yaml", reading.Document.Source.File);
    }

    [Fact, Trait("Lane", "unit")]
    public void On_yes_and_no_stay_text_under_the_core_schema_while_true_and_numbers_are_typed()
    {
        var recipe = Bind(Minimal.Replace("step: step.produce", "step: { use: step.produce, with: { a: on, b: yes, c: no, d: true, e: 12, f: 1.5, g: '7', h: ~ } }", StringComparison.Ordinal));
        var with = recipe.States["produce"].Step!.With;

        Assert.Equal(JsonValueKind.String, with["a"].Literal!.Value.ValueKind);
        Assert.Equal("on", with["a"].Literal!.Value.GetString());
        Assert.Equal("yes", with["b"].Literal!.Value.GetString());
        Assert.Equal("no", with["c"].Literal!.Value.GetString());
        Assert.Equal(JsonValueKind.True, with["d"].Literal!.Value.ValueKind);
        Assert.Equal(12, with["e"].Literal!.Value.GetInt32());
        Assert.Equal(1.5, with["f"].Literal!.Value.GetDouble());
        Assert.Equal("7", with["g"].Literal!.Value.GetString());
        Assert.Null(with["h"].Literal);
        Assert.Equal("step.completed", recipe.Transitions[0].On);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_node_position_is_recorded_as_written()
    {
        var positions = Reader.Read(Minimal, "test.yaml").Document!.Source.Positions;

        Assert.Equal(new SourcePosition(1, 1), positions.Locate(string.Empty));
        Assert.Equal(new SourcePosition(2, 1), positions.Locate("id"));
        Assert.Equal(new SourcePosition(6, 3), positions.Locate("states.produce"));
        Assert.Equal(new SourcePosition(8, 5), positions.Locate("states.produce.step"));
        Assert.Equal(new SourcePosition(9, 16), positions.Locate("states.produce.timeout.after"));
        Assert.Equal(new SourcePosition(15, 5), positions.Locate("transitions[0]"));
        Assert.Equal(new SourcePosition(15, 7), positions.Locate("transitions[0].from"));
        Assert.Equal(new SourcePosition(15, 88), positions.Locate("transitions[0].produces[0]"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_short_forms_long_children_locate_at_the_short_form()
    {
        var positions = Reader.Read(Minimal, "test.yaml").Document!.Source.Positions;

        Assert.Equal(positions.Locate("states.produce.step"), positions.Locate("states.produce.step.use"));
        Assert.Equal(positions.Locate("transitions[0].from"), positions.Locate("transitions[0].from[0]"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_short_form_normalizes_to_the_long_form()
    {
        var recipe = Reader.Read(RecipeFixtures.Read("valid/short-forms.yaml"), "short-forms.yaml").Document!.Recipe;
        var review = recipe.States["review"];

        Assert.Equal([new RecipeInput(FactKey.From("fact:item"), Label: null, Type: null)], recipe.Inputs);
        Assert.Equal("step.produce", recipe.States["produce"].Step!.Use.Value);
        Assert.Empty(recipe.States["produce"].Step!.With);
        Assert.Equal("input.context.retrieve", Assert.Single(review.Step!.Inputs).Use.Value);
        Assert.Equal(["produce"], recipe.Transitions[0].From);
        Assert.Equal("gate.budget.available", Assert.Single(recipe.Transitions[0].Gates).Use.Value);
        Assert.Null(recipe.Transitions[0].Gates[0].Threshold);
        Assert.Equal("effect.notify.desk", Assert.Single(recipe.Transitions[0].Effects).Use.Value);
        Assert.Empty(recipe.Transitions[1].Produces);
        Assert.Equal(200_000, recipe.States["produce"].Budget!.Tokens);
        Assert.Equal(2_000_000, recipe.Budget!.Tokens);
        Assert.True(review.Step.With["strict"].Literal!.Value.GetBoolean());
        Assert.Equal("write it", recipe.States["running"].WaitFor!.Child!.Inputs[FactKey.From("fact:spec")].Literal!.Value.GetString());
        Assert.Equal(LoopSpec.DefaultNoProgress, recipe.Loops["revisions"].NoProgress);
        Assert.Empty(recipe.Evals);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_violation_is_reported_not_just_the_first()
    {
        var yaml = Minimal
            .Replace("kind: active", "kind: activ", StringComparison.Ordinal)
            .Replace("next: \"Produce the Step output.\"", "next: \"Produce the Step output.\"\n    colour: blue", StringComparison.Ordinal)
            .Replace("after: 4h", "after: 4 hours", StringComparison.Ordinal)
            .Replace("version: 1", "version: one", StringComparison.Ordinal);

        var findings = Reader.Read(yaml, "test.yaml");

        Assert.Null(findings.Document);
        Assert.Equal(
            [
                "RCP-SCHEMA-002: 'one' isn't a whole number at test.yaml:3:1 (version); write a whole number",
                "RCP-SCHEMA-002: 'activ' isn't active, wait or terminal at test.yaml:7:5 (states.produce.kind); write active, wait or terminal",
                "RCP-DUR-001: '4 hours' isn't a duration ('10m', '4h', '2d', at least 1m) at test.yaml:9:16 (states.produce.timeout.after); write a whole number and m, h or d, at least 1m ('10m', '4h', '2d')",
                "RCP-SCHEMA-001: 'colour' isn't a field of a state at test.yaml:11:5 (states.produce.colour); remove it; a state has kind, step, waitFor, timeout, loop, budget, next, outcome",
            ],
            findings.Findings.Select(f => f.ToLine()),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_required_field_is_reported_where_its_parent_starts()
    {
        var lines = Lines(Minimal.Replace("initial: produce\n", string.Empty, StringComparison.Ordinal).Replace("to: done, ", string.Empty, StringComparison.Ordinal));

        Assert.Equal(
            [
                "RCP-SCHEMA-001: a transition has no 'to' at test.yaml:14:5 (transitions[0].to); add 'to': text",
                "RCP-SCHEMA-001: the recipe has no 'initial' at test.yaml:1:1 (initial); add 'initial': text",
            ],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_list_written_where_a_mapping_belongs_is_a_wrong_type()
    {
        var lines = Lines(Minimal.Replace("timeout: { after: 4h, then: park }", "timeout: [4h, park]", StringComparison.Ordinal));

        Assert.Equal(["RCP-SCHEMA-002: a timeout is written as a list at test.yaml:9:5 (states.produce.timeout); write a timeout as a mapping"], lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bad_token_count_and_a_literal_list_of_mappings_are_wrong_types()
    {
        var lines = Lines(Minimal
            .Replace("    next: \"Produce", "    budget: { tokens: lots }\n    next: \"Produce", StringComparison.Ordinal)
            .Replace("step: step.produce", "step: { use: step.produce, with: { files: [{ a: 1 }] } }", StringComparison.Ordinal));

        Assert.Equal(
            [
                "RCP-SCHEMA-002: the list 'files' holds more than strings, numbers and bools at test.yaml:8:40 (states.produce.step.with.files); a literal list holds only strings, numbers and bools; use { fact } or { profile } for anything else",
                "RCP-SCHEMA-002: 'lots' isn't a token count at test.yaml:10:15 (states.produce.budget.tokens); write a whole number of at least 1, or one with k or M ('1500', '200k', '2M')",
            ],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_isnt_a_mapping_is_a_wrong_type() =>
        Assert.Equal(
            ["RCP-SCHEMA-002: the recipe isn't a mapping at test.yaml:1:1 (); write the recipe as a YAML mapping with schema, id, version, initial, states and transitions"],
            Lines("- just\n- a list\n"));
}
