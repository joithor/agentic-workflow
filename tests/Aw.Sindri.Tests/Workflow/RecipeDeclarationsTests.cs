using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The declarations of the UI screens (spec §3.1, §3.2, §3.3; UI E-21, E-22): a recipe's 'urgency', its 'outputs' and an
/// input's 'type' and 'kind' read into the long form, an absent urgency is written as 'work', and a recipe that says
/// nothing about them hashes the same as one that says the defaults.
/// </summary>
public sealed class RecipeDeclarationsTests
{
    private const string Declared =
        "urgency: triage\n"
        + "inputs:\n"
        + "  - { key: fact:item, label: Item, type: item, kind: item }\n"
        + "outputs:\n"
        + "  - { key: fact:output, label: The answer, type: file, from: produce }\n"
        + "  - { key: fact:note, from: produce }\n";

    private static string WithDeclarations(string declarations) =>
        RecipeReaderTests.Minimal.Replace("initial: produce\n", declarations + "initial: produce\n", StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Urgency_inputs_and_outputs_read_into_the_long_form()
    {
        var recipe = RecipeReaderTests.Bind(WithDeclarations(Declared));

        Assert.Equal("triage", recipe.Urgency);
        Assert.Equal([new RecipeInput(Core.Workflow.Ids.FactKey.From("fact:item"), "Item", "item", "item", Generate: null)], recipe.Inputs);
        Assert.Equal(
            [
                new RecipeOutput(Core.Workflow.Ids.FactKey.From("fact:output"), "The answer", "file", "produce"),
                new RecipeOutput(Core.Workflow.Ids.FactKey.From("fact:note"), Label: null, Type: null, "produce"),
            ],
            recipe.Outputs);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_absent_urgency_is_written_as_work_and_outputs_are_empty()
    {
        var recipe = RecipeReaderTests.Bind(RecipeReaderTests.Minimal);

        Assert.Equal("work", recipe.Urgency);
        Assert.Empty(recipe.Outputs);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_absent_urgency_hashes_the_same_as_urgency_work()
    {
        var absent = RecipeReaderTests.Reader.Read(RecipeReaderTests.Minimal, "a.yaml").Document!.Hash;
        var stated = RecipeReaderTests.Reader.Read(WithDeclarations("urgency: work\n"), "b.yaml").Document!.Hash;
        var other = RecipeReaderTests.Reader.Read(WithDeclarations("urgency: incident\n"), "c.yaml").Document!.Hash;

        Assert.Equal(absent, stated);
        Assert.NotEqual(absent, other);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_output_with_an_unknown_field_is_a_schema_finding()
    {
        var lines = RecipeReaderTests.Lines(WithDeclarations("outputs:\n  - { key: fact:output, from: produce, color: red }\n"));

        Assert.Contains(lines, l => l.Contains("RCP-SCHEMA-001", StringComparison.Ordinal) && l.Contains("outputs[0].color", StringComparison.Ordinal));
    }

    internal static JsonObject LegacyJson(string yaml) => LegacyRecipeRows.Json(yaml);

    internal static Core.Workflow.Ids.RecipeHash LegacyHash(string yaml) => LegacyRecipeRows.Hash(yaml);

    [Fact, Trait("Lane", "unit")]
    public void Legacy_stored_text_without_the_declarations_reads_with_their_defaults()
    {
        var yaml = WithDeclarations("inputs: [fact:item]\n");
        var row = new Core.Workflow.Registry.RegisteredRecipe(
            Core.Workflow.Ids.RecipeId.From("workflow.minimal"), 1, LegacyHash(yaml), LegacyJson(yaml).ToJsonString(), Base: null, DateTimeOffset.UnixEpoch);

        var read = row.Read(SliceHarness.Json);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.ToString() : null);
        Assert.Equal(AwJson.Serialize(RecipeReaderTests.Bind(yaml), SliceHarness.Json), AwJson.Serialize(read.Value, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_new_long_form_is_the_legacy_one_plus_urgency_outputs_and_kind_so_every_old_hash_changes()
    {
        var yaml = WithDeclarations("inputs: [fact:item]\n");
        var legacy = LegacyJson(yaml);
        var current = JsonNode.Parse(AwJson.Serialize(RecipeReaderTests.Bind(yaml) with { Hash = null }, SliceHarness.Json))!.AsObject();

        Assert.Equal(["displayName", "name", "outputs", "urgency"], current.Select(p => p.Key).Except(legacy.Select(p => p.Key), StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.NotEqual(LegacyHash(yaml), RecipeHashing.Of(RecipeReaderTests.Bind(yaml), SliceHarness.Json));
    }
}
