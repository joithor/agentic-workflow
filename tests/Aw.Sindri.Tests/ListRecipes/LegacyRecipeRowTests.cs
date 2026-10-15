using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApproveRecipe;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ListRecipes;

/// <summary>
/// A registry row from before urgency, outputs and an input's kind (spec §3.1): its text and hash stay as registered,
/// it still lists and shows with the defaults, and the same file registered again hashes differently, so its id and
/// version are taken (SND-RECIPE-007 naming RCP-VERSION-001) until the author bumps the version.
/// </summary>
public sealed class LegacyRecipeRowTests
{
    private static async Task<(RegistryHarness Registry, RegisteredRecipe Row)> Seeded()
    {
        var registry = new RegistryHarness();
        _ = await registry.H.HoldLease();
        var legacy = RecipeDeclarationsTests.LegacyJson(ShippedRecipes.Read("sindri/workflow.step.yaml"));
        var row = new RegisteredRecipe(RecipeId.From("workflow.step"), 1, RecipeHash.Of(CanonicalJson.Of(legacy)), legacy.ToJsonString(), Base: null, registry.H.Clock.GetUtcNow());
        _ = await registry.Recipes.Add(row, TestContext.Current.CancellationToken);
        return (registry, row);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_old_row_still_lists_with_its_stored_hash_and_the_defaults()
    {
        var (r, row) = await Seeded();
        await using var _ = r;

        var line = await r.Line(row);

        Assert.Equal(row.Hash, line.Hash);
        Assert.Equal("work", line.Urgency);
        Assert.Empty(line.Outputs);
        Assert.Equal(["fact:spec"], line.Inputs.Select(i => i.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Registering_the_same_file_again_names_the_taken_version()
    {
        var (r, row) = await Seeded();
        await using var _ = r;

        var again = await r.RegisterShipped("sindri/workflow.step.yaml");

        Assert.True(again.IsError);
        Assert.Equal(WorkflowErrors.RecipeVersionTaken.Code, again.Error.Code);
        Assert.Contains("RCP-VERSION-001", again.Error.Message, StringComparison.Ordinal);
        Assert.Equal([row.Hash], (await r.List()).Select(s => s.Hash));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_listing_of_a_new_recipe_carries_its_urgency_and_outputs()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var yaml = RecipeReaderTests.Minimal.Replace("initial: produce\n", "urgency: incident\noutputs: [{ key: fact:output, type: file, from: produce }]\ninitial: produce\n", StringComparison.Ordinal);
        var registered = await r.RegisterText(yaml, "minimal.yaml");
        Assert.True(registered.IsSuccess, registered.IsError ? registered.Error.ToString() : null);

        var line = Assert.Single(await r.List());

        Assert.Equal("incident", line.Urgency);
        Assert.Equal(["fact:output"], line.Outputs.Select(o => o.Key.Value), StringComparer.Ordinal);
        Assert.Equal("produce", Assert.Single(line.Outputs).From);
    }
}
