using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Tests.ApproveRecipe;

namespace Aw.Sindri.Tests.ShowRecipe;

/// <summary>
/// 'recipe show &lt;id@hash&gt;' (engine spec §4.2, §6.3; query): the recipe's line and the preview approving it would
/// show, by the full hash or a prefix of 8 or more digits, whether or not it is approved.
/// </summary>
public sealed class ShowRecipeTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Show_returns_the_line_and_the_approval_preview_by_a_hash_prefix()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var shown = await r.Show(step.Id, step.Hash.Display);

        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        var (line, preview) = (shown.Value.Recipe, shown.Value.Preview);
        Assert.Equal((step.Hash, RecipeStanding.Unapproved), (line.Hash, line.Standing));
        Assert.Equal((step.Id, 2, step.Hash), (preview.Id, preview.Version, preview.Hash));
        Assert.Equal(["step.checks", "step.produce", "step.verify"], preview.Pins.Blocks.Select(b => b.Block.Value), StringComparer.Ordinal);
        Assert.Contains("3 declared", preview.Evals, StringComparison.Ordinal);
        Assert.Null(preview.Diff);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_recipe_still_shows_its_preview_and_the_pins_it_holds()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var approved = (await r.Approve(step)).Value;

        var shown = await r.Show(step.Id, step.Hash.Value);

        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        Assert.Equal((RecipeStanding.Approved, (ApprovalVia?)ApprovalVia.Desk), (shown.Value.Recipe.Standing, shown.Value.Recipe.Via));
        Assert.Equal(approved.Approval.Pins, shown.Value.Preview.Pins);
        Assert.False(shown.Value.Preview.IsDrift);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_revoked_recipe_shows_revoked()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);
        _ = await r.Approvals.Revoke(Testing.Workflow.Registry.RegistryData.Revocation(step), TestContext.Current.CancellationToken);

        var shown = await r.Show(step.Id, step.Hash.Display);

        Assert.Equal(RecipeStanding.Revoked, shown.Value.Recipe.Standing);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.step", "0000000000000000")]
    [InlineData("workflow.other", null)]
    public async Task An_unknown_id_or_hash_is_SND_RECIPE_002(string id, string? hash)
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var shown = await r.Show(RecipeId.From(id), hash ?? step.Hash.Display);

        Assert.Equal(WorkflowErrors.RecipeNotFound.Code, shown.Error.Code);
        Assert.Contains("sindri recipe list", shown.Error.Message, StringComparison.Ordinal);
    }
}
