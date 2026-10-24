using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Xunit;

namespace Aw.Sindri.Testing.Workflow.Registry;

/// <summary>
/// The contract every <see cref="IRecipeApprovals"/> honours (engine spec §4.1, W8, W11): approvals keyed by (id, version,
/// hash), a drift approval appends a row, a revoked hash can't be re-approved, a revoke is found and is idempotent, and
/// the cascade and child-pin listings skip revoked rows. Run against the fake and the SQLite adapter (P2 unit (a)).
/// </summary>
public abstract class RecipeApprovalsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IRecipeApprovals Create();

    /// <summary>An approval is found by its key, and only by its key.</summary>
    [Fact]
    public async Task Approval_is_found_by_id_version_and_hash()
    {
        var approvals = Create();
        var step = RegistryData.Recipe("workflow.step");
        var approval = RegistryData.Approval(step);
        _ = await approvals.Approve(approval, Token);

        Expect.Equal(approval, (await approvals.Latest(step.Id, 1, step.Hash, Token)).Value);
        Expect.True((await approvals.Latest(step.Id, 2, step.Hash, Token)).Value is null);
    }

    /// <summary>Re-approving the same hash with new pins appends a row; Latest has the new pins and History both.</summary>
    [Fact]
    public async Task Drift_approval_appends_a_row_with_the_new_pins()
    {
        var approvals = Create();
        var step = RegistryData.Recipe("workflow.step");
        var first = RegistryData.Approval(step, new RecipePins([new BlockPin(BlockId.From("step.produce"), RegistryData.Descriptor("v1"))], []));
        var drift = RegistryData.Approval(step, new RecipePins([new BlockPin(BlockId.From("step.produce"), RegistryData.Descriptor("v2"))], []));
        _ = await approvals.Approve(first, Token);
        _ = await approvals.Approve(drift, Token);

        Expect.Equal(drift.Pins, (await approvals.Latest(step.Id, 1, step.Hash, Token)).Value!.Pins);
        Expect.Equal(2, (await approvals.History(step.Id, 1, step.Hash, Token)).Value.Count);
    }

    /// <summary>A block pin keeps its interface hash through the store; one recorded without it reads back without (owner q93, 2026-10-23).</summary>
    [Fact]
    public async Task Block_pins_keep_their_interface_hash()
    {
        var approvals = Create();
        var step = RegistryData.Recipe("workflow.step");
        var pins = new RecipePins(
            [new BlockPin(BlockId.From("step.produce"), RegistryData.Descriptor("v1"), RegistryData.Descriptor("shape")), new BlockPin(BlockId.From("step.verify"), RegistryData.Descriptor("v1"))], []);
        _ = await approvals.Approve(RegistryData.Approval(step, pins), Token);

        var read = (await approvals.Latest(step.Id, 1, step.Hash, Token)).Value!.Pins.Blocks;

        Expect.Equal(RegistryData.Descriptor("shape"), read[0].Interface);
        Expect.True(read[1].Interface is null);
    }

    /// <summary>A revoked hash can't be approved again (SND-RECIPE-005), and its revocation is found.</summary>
    [Fact]
    public async Task Revoked_hash_cannot_be_approved_again()
    {
        var approvals = Create();
        var step = RegistryData.Recipe("workflow.step");
        _ = await approvals.Approve(RegistryData.Approval(step), Token);
        _ = await approvals.Revoke(RegistryData.Revocation(step), Token);

        var again = await approvals.Approve(RegistryData.Approval(step), Token);

        Expect.Equal(WorkflowErrors.RecipeRevoked.Code, again.Error.Code);
        Expect.Equal(RegistryData.Revocation(step), (await approvals.Revocation(step.Hash, Token)).Value);
    }

    /// <summary>Revoking twice returns the first row; revoking a hash never approved is SND-RECIPE-003.</summary>
    [Fact]
    public async Task Revoke_is_idempotent_and_needs_an_approval()
    {
        var approvals = Create();
        var step = RegistryData.Recipe("workflow.step");
        _ = await approvals.Approve(RegistryData.Approval(step), Token);
        var first = (await approvals.Revoke(RegistryData.Revocation(step), Token)).Value;

        Expect.Equal(first, (await approvals.Revoke(RegistryData.Revocation(step) with { RevokedBy = "human:ui" }, Token)).Value);
        Expect.Equal(WorkflowErrors.RecipeNotApproved.Code, (await approvals.Revoke(RegistryData.Revocation(RegistryData.Recipe("workflow.item")), Token)).Error.Code);
    }

    /// <summary>Pinning lists the parents of a child hash and Current the live approvals; both skip revoked rows.</summary>
    [Fact]
    public async Task Pinning_and_current_listings_skip_revoked_rows()
    {
        var approvals = Create();
        var step = RegistryData.Recipe("workflow.step");
        var generated = RegistryData.Recipe("workflow.step-gen", baseRecipe: new RecipeRef(step.Id, step.Hash));
        var revokedGenerated = RegistryData.Recipe("workflow.step-old", baseRecipe: new RecipeRef(step.Id, step.Hash));
        var item = RegistryData.Recipe("workflow.item");
        foreach (var approval in new[]
        {
            RegistryData.Approval(step), RegistryData.Approval(generated), RegistryData.Approval(revokedGenerated),
            RegistryData.Approval(item, new RecipePins([], [new ChildPin(step.Id, step.Hash)])),
        })
        {
            _ = await approvals.Approve(approval, Token);
        }

        _ = await approvals.Revoke(RegistryData.Revocation(revokedGenerated, step.Hash), Token);

        Expect.Sequence([item.Hash], (await approvals.Pinning(step.Hash, Token)).Value.Select(a => a.Hash));
        Expect.Sequence(["workflow.item", "workflow.step", "workflow.step-gen"], (await approvals.Current(Token)).Value.Select(a => a.Id.Value));
    }

    /// <summary>GeneratedFrom lists every generated recipe approved from a base, revoked ones too: the edges a revoke walks through.</summary>
    [Fact]
    public async Task Generated_listing_keeps_revoked_rows()
    {
        var approvals = Create();
        var step = RegistryData.Recipe("workflow.step");
        var generated = RegistryData.Recipe("workflow.step-gen", baseRecipe: new RecipeRef(step.Id, step.Hash));
        var revokedGenerated = RegistryData.Recipe("workflow.step-old", baseRecipe: new RecipeRef(step.Id, step.Hash));
        foreach (var recipe in new[] { step, generated, revokedGenerated })
        {
            _ = await approvals.Approve(RegistryData.Approval(recipe), Token);
        }

        var repinned = RegistryData.Approval(generated, new RecipePins([], [new ChildPin(step.Id, step.Hash)]));
        _ = await approvals.Approve(repinned, Token);   // a drift re-approval: the same key, a newer row
        _ = await approvals.Revoke(RegistryData.Revocation(revokedGenerated, step.Hash), Token);

        var listed = (await approvals.GeneratedFrom(step.Hash, Token)).Value;
        Expect.Sequence([generated.Hash, revokedGenerated.Hash], listed.Select(a => a.Hash));   // each key once …
        Expect.Equal(repinned.Pins, listed[0].Pins);                                            // … at its latest row
        Expect.Sequence([], (await approvals.GeneratedFrom(generated.Hash, Token)).Value.Select(a => a.Hash));
    }
}
