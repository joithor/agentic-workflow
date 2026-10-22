using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The plain-words fields (setup-once spec §5.3): a recipe's displayName, a state's why and displayName and a transition's label read into the long form and join the hash.</summary>
public sealed class RecipeDisplayFieldsTests
{
    private const string Named =
        "schema: 1\n"
        + "id: workflow.named\n"
        + "version: 2\n"
        + "displayName: Produce and finish\n"
        + "inputs: [{ key: fact:item, type: item, kind: item }]\n"
        + "initial: produce\n"
        + "states:\n"
        + "  produce:\n"
        + "    kind: active\n"
        + "    step: step.produce\n"
        + "    timeout: { after: 4h, then: park }\n"
        + "    next: \"Produce the Step output.\"\n"
        + "    displayName: Write the draft\n"
        + "    why: An AI writes the draft so you have something concrete to approve.\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n"
        + "  - { from: produce, to: done, on: step.completed, when: { outcome: done }, label: \"all done\", produces: [fact:output] }\n";

    [Fact, Trait("Lane", "unit")]
    public void The_display_name_why_state_name_and_label_read_into_the_long_form()
    {
        var recipe = RecipeReaderTests.Bind(Named);

        Assert.Equal("Produce and finish", recipe.DisplayName);
        Assert.Equal("Write the draft", recipe.States["produce"].DisplayName);
        Assert.Equal("An AI writes the draft so you have something concrete to approve.", recipe.States["produce"].Why);
        Assert.Equal("all done", Assert.Single(recipe.Transitions).Label);
        Assert.Null(recipe.States["done"].Why);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_plain_words_field_changes_the_hash()
    {
        var hash = RecipeReaderTests.Reader.Read(Named, "n.yaml").Document!.Hash;

        foreach (var (from, to) in new[]
        {
            ("displayName: Produce and finish", "displayName: Produce and end"),
            ("displayName: Write the draft", "displayName: Write a draft"),
            ("why: An AI writes", "why: The AI writes"),
            ("label: \"all done\"", "label: \"finished\""),
        })
        {
            var changed = RecipeReaderTests.Reader.Read(Named.Replace(from, to, StringComparison.Ordinal), "n.yaml").Document!.Hash;

            Assert.NotEqual(hash, changed);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_without_why_or_name_and_a_transition_without_a_label_write_nothing_into_the_hashed_form()
    {
        var plain = Named
            .Replace("    displayName: Write the draft\n", string.Empty, StringComparison.Ordinal)
            .Replace("    why: An AI writes the draft so you have something concrete to approve.\n", string.Empty, StringComparison.Ordinal)
            .Replace(" label: \"all done\",", string.Empty, StringComparison.Ordinal);

        var recipe = RecipeReaderTests.Reader.Read(plain, "n.yaml").Document!.Recipe;
        var json = Encoding.UTF8.GetString(CanonicalJson.Of(JsonNode.Parse(AwJson.Serialize(recipe, SliceHarness.Json))));

        Assert.DoesNotContain("\"why\"", json, StringComparison.Ordinal);
        Assert.False(JsonNode.Parse(json)!["transitions"]![0]!.AsObject().ContainsKey("label"));   // why: the recipe has one transition
        Assert.Contains("\"displayName\":\"Produce and finish\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Write the draft", json, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_file_without_a_display_name_binds_and_the_validator_reports_RCP_DISPLAY_001()
    {
        var yaml = Named.Replace("displayName: Produce and finish\n", string.Empty, StringComparison.Ordinal);
        var reading = RecipeReaderTests.Reader.Read(yaml, "n.yaml");

        Assert.Empty(reading.Findings);
        Assert.Null(reading.Document!.Recipe.DisplayName);
        Assert.Contains(
            RecipeComposition.Errors(reading.Document, new BlockCatalog([TestBlocks.Step("step.produce")], []), InMemoryChildRecipes.None),
            line => line.StartsWith("RCP-DISPLAY-001: the recipe has no displayName", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_why_and_a_states_on_outcome_read_into_the_long_form_and_join_the_hash()
    {
        var yaml = Named
            .Replace("displayName: Produce and finish\n", "displayName: Produce and finish\nwhy: Gets the work from idea to done.\n", StringComparison.Ordinal)
            .Replace("    displayName: Write the draft\n", "    displayName: Write the draft\n    onOutcome: { done: \"Moves on to the end.\" }\n", StringComparison.Ordinal);

        var reading = RecipeReaderTests.Reader.Read(yaml, "n.yaml");

        Assert.Empty(reading.Findings);
        Assert.Equal("Gets the work from idea to done.", reading.Document!.Recipe.Why);
        Assert.Equal("Moves on to the end.", reading.Document.Recipe.States["produce"].OnOutcome!["done"]);
        Assert.Null(reading.Document.Recipe.States["done"].OnOutcome);
        Assert.NotEqual(RecipeReaderTests.Reader.Read(Named, "n.yaml").Document!.Hash, reading.Document.Hash);
        Assert.NotEqual(reading.Document.Hash, RecipeReaderTests.Reader.Read(yaml.Replace("Moves on to the end.", "Ends it.", StringComparison.Ordinal), "n.yaml").Document!.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_on_outcome_that_is_not_a_mapping_of_texts_is_a_reader_finding()
    {
        var yaml = Named.Replace("    displayName: Write the draft\n", "    displayName: Write the draft\n    onOutcome: [done]\n", StringComparison.Ordinal);

        Assert.NotEmpty(RecipeReaderTests.Reader.Read(yaml, "n.yaml").Findings);
    }
}
