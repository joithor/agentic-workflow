using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.RevokeRecipe;
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
        var source = LegacyStep();
        var row = new RegisteredRecipe(RecipeId.From("workflow.step"), 1, RecipeDeclarationsTests.LegacyHash(source),
            RecipeDeclarationsTests.LegacyJson(source).ToJsonString(), Base: null, registry.H.Clock.GetUtcNow());
        _ = await registry.Recipes.Add(row, TestContext.Current.CancellationToken);
        return (registry, row);
    }

    /// <summary>workflow.step as it was registered before P2b: the file with its inputs in the bare-key short form it had then.</summary>
    private static string LegacyStep() => ShippedRecipes.Read("sindri/workflow.step.yaml")
        .Replace("inputs:\n  - { key: fact:spec, label: \"Spec\", type: text, kind: text }\n", "inputs: [fact:spec]\n", StringComparison.Ordinal);

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
        var yaml = RecipeReaderTests.Minimal.Replace("initial: produce\n", "urgency: incident\ninputs: [{ key: fact:item, type: item, kind: item }]\noutputs: [{ key: fact:output, type: file, from: produce }]\ninitial: produce\n", StringComparison.Ordinal);
        var registered = await r.RegisterText(yaml, "minimal.yaml");
        Assert.True(registered.IsSuccess, registered.IsError ? registered.Error.ToString() : null);

        var line = Assert.Single(await r.List());

        Assert.Equal("incident", line.Urgency);
        Assert.Equal(["fact:output"], line.Outputs.Select(o => o.Key.Value), StringComparer.Ordinal);
        Assert.Equal("produce", Assert.Single(line.Outputs).From);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_listing_of_a_recipe_carries_its_path_order_and_loops()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var registered = await r.RegisterShipped("sindri/workflow.step.yaml");
        Assert.True(registered.IsSuccess, registered.IsError ? registered.Error.ToString() : null);

        var line = Assert.Single(await r.List());

        Assert.Equal("path", line.OrderedBy);
        Assert.Equal(["produce=1", "checks=2", "verify=3", "done=4"], line.States.Select(s => $"{s.State}={s.Order?.ToString(System.Globalization.CultureInfo.InvariantCulture)}"), StringComparer.Ordinal);
        Assert.Equal(["verify>produce", "checks>produce"], line.Loops.Select(l => $"{l.From}>{l.To}"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_legacy_hash_of_workflow_step_is_the_one_it_was_registered_under()
    {
        Assert.Equal(
            "sha256:12e030dc92a0f827d4da641c65f023d5e547e15e9c1b29a9a427a603e0f50d40",
            RecipeDeclarationsTests.LegacyHash(LegacyStep()).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_old_row_lists_approved_and_not_stale_shows_its_preview_and_revokes_by_its_stored_hash()
    {
        var (r, row) = await Seeded();
        await using var _ = r;

        var approved = await r.Approve(row);
        var line = await r.Line(row);
        var shown = await r.Show(row.Id, row.Hash.Display);
        var issued = (await r.Issue($"revoke workflow.step@{row.Hash.Display}")).Value;
        var revoked = await r.Run<RevokeRecipeRequest, RevokeRecipeResponse>(
            new RevokeRecipeRequest(row.Id, row.Hash, new ChallengeAnswer(issued.Challenge, issued.Action)), RegistryHarness.Human);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        Assert.Equal(row.Hash, approved.Value.Approval.Hash);
        Assert.Equal((RecipeStanding.Approved, 0), (line.Standing, line.Stale.Count));
        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        Assert.Equal(row.Hash, shown.Value.Recipe.Hash);
        Assert.True(revoked.IsSuccess, revoked.IsError ? revoked.Error.ToString() : null);
        Assert.Equal(row.Hash, revoked.Value.Revocation.Hash);
        Assert.Equal(RecipeStanding.Revoked, (await r.Line(row)).Standing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_diff_against_an_approved_old_row_shows_only_what_the_author_changed()
    {
        var (r, row) = await Seeded();
        await using var _ = r;
        var approved = await r.Approve(row);
        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        var next = await r.Register(RecipeReaderTests.Bind(LegacyStep()) with { Version = 2, Description = "A reworded step." });

        var shown = await r.Show(next.Id, next.Hash.Display);

        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        var changed = shown.Value.Preview.Diff!.Split('\n').Where(l => (l.StartsWith('+') || l.StartsWith('-')) && !l.StartsWith("+++", StringComparison.Ordinal) && !l.StartsWith("---", StringComparison.Ordinal)).ToList();
        Assert.Contains(changed, l => l.Contains("A reworded step.", StringComparison.Ordinal));
        Assert.DoesNotContain(changed, l => l.Contains("urgency", StringComparison.Ordinal) || l.Contains("outputs", StringComparison.Ordinal) || l.Contains("\"kind\": null", StringComparison.Ordinal));
    }
}
