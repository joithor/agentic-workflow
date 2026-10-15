using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.Registry;

/// <summary>
/// The registry's events on the ledger chain (engine spec §4.1): 'recipe.registered:&lt;hash&gt;',
/// 'recipe.approved:&lt;hash&gt;:after:&lt;seq&gt;' and 'recipe.revoked:&lt;hash&gt;', each with its payload (id, version,
/// hash; the pins and whether it is a drift approval; the base a revoke cascaded from), written under the held lease.
/// </summary>
public sealed class RecipeEventsTests
{
    private readonly SliceHarness _h = new();

    private RecipeEvents Events => new(_h.Store, _h.Events, _h.Held);

    [Fact, Trait("Lane", "unit")]
    public async Task Registering_appends_recipe_registered_keyed_by_the_hash_once()
    {
        _ = await _h.HoldLease();
        var recipe = RegistryData.Recipe("workflow.step", 2);

        var first = await Events.Registered(recipe, TestContext.Current.CancellationToken);
        var again = await Events.Registered(recipe, TestContext.Current.CancellationToken);

        var recorded = Assert.Single(await Recorded(EventTypes.RecipeRegistered));
        Assert.Equal($"recipe.registered:{recipe.Hash.Value}", recorded.IdempotencyKey);
        Assert.Equal(new RecipeRegistered(recipe.Id, 2, recipe.Hash), _h.Events.Payload<RecipeRegistered>(recorded).Value);
        Assert.Equal((recorded.Sequence, false), (first.Value.Sequence, first.Value.AlreadyRecorded));
        Assert.Equal((recorded.Sequence, true), (again.Value.Sequence, again.Value.AlreadyRecorded));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_appends_recipe_approved_with_the_pins_keyed_after_the_latest_approval()
    {
        _ = await _h.HoldLease();
        var recipe = RegistryData.Recipe("workflow.step");
        var approval = RegistryData.Approval(recipe, Pins("a"));

        var appended = await Events.Approved(approval, drift: false, TestContext.Current.CancellationToken);

        var recorded = Assert.Single(await Recorded(EventTypes.RecipeApproved));
        Assert.Equal($"recipe.approved:{recipe.Hash.Value}:after:0", recorded.IdempotencyKey);
        Assert.Equal(recorded.Sequence, appended.Value.Sequence);
        var payload = _h.Events.Payload<RecipeApproved>(recorded).Value;
        Assert.Equal((recipe.Id, 1, recipe.Hash, ApprovalVia.Desk, default(RecipeRef), false), (payload.Id, payload.Version, payload.Hash, payload.Via, payload.Base, payload.Drift));
        Assert.Equal(approval.Pins.Blocks, payload.Pins.Blocks);
        Assert.Empty(payload.Pins.Children);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_drift_approval_of_the_same_hash_is_its_own_event()
    {
        _ = await _h.HoldLease();
        var recipe = RegistryData.Recipe("workflow.step");
        var first = await Events.Approved(RegistryData.Approval(recipe, Pins("a")), drift: false, TestContext.Current.CancellationToken);

        var drift = await Events.Approved(RegistryData.Approval(recipe, Pins("b")), drift: true, TestContext.Current.CancellationToken);

        var recorded = await Recorded(EventTypes.RecipeApproved);
        Assert.Equal(2, recorded.Count);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"recipe.approved:{recipe.Hash.Value}:after:{first.Value.Sequence}"), recorded[1].IdempotencyKey);
        Assert.Equal((recorded[1].Sequence, false), (drift.Value.Sequence, drift.Value.AlreadyRecorded));
        var payload = _h.Events.Payload<RecipeApproved>(recorded[1]).Value;
        Assert.True(payload.Drift);
        Assert.Equal(Pins("b").Blocks, payload.Pins.Blocks);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_retried_approval_that_is_already_the_latest_event_records_nothing()
    {
        _ = await _h.HoldLease();
        var approval = RegistryData.Approval(RegistryData.Recipe("workflow.step"), Pins("a"));
        var first = await Events.Approved(approval, drift: false, TestContext.Current.CancellationToken);

        var retry = await Events.Approved(approval, drift: false, TestContext.Current.CancellationToken);

        _ = Assert.Single(await Recorded(EventTypes.RecipeApproved));
        Assert.Equal((first.Value.Sequence, true), (retry.Value.Sequence, retry.Value.AlreadyRecorded));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revoking_appends_recipe_revoked_with_the_base_it_cascaded_from()
    {
        _ = await _h.HoldLease();
        var baseRecipe = RegistryData.Recipe("workflow.step");
        var generated = RegistryData.Recipe("workflow.step-generated", baseRecipe: new RecipeRef(baseRecipe.Id, baseRecipe.Hash));

        _ = await Events.Revoked(RegistryData.Revocation(baseRecipe), TestContext.Current.CancellationToken);
        _ = await Events.Revoked(RegistryData.Revocation(generated, cascadedFrom: baseRecipe.Hash), TestContext.Current.CancellationToken);
        var again = await Events.Revoked(RegistryData.Revocation(baseRecipe), TestContext.Current.CancellationToken);

        var recorded = await Recorded(EventTypes.RecipeRevoked);
        Assert.Equal([$"recipe.revoked:{baseRecipe.Hash.Value}", $"recipe.revoked:{generated.Hash.Value}"], recorded.Select(r => r.IdempotencyKey), StringComparer.Ordinal);
        Assert.Equal(new RecipeRevoked(baseRecipe.Id, 1, baseRecipe.Hash, CascadedFrom: null), _h.Events.Payload<RecipeRevoked>(recorded[0]).Value);
        Assert.Equal(new RecipeRevoked(generated.Id, 1, generated.Hash, baseRecipe.Hash), _h.Events.Payload<RecipeRevoked>(recorded[1]).Value);
        Assert.True(again.Value.AlreadyRecorded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_held_lease_nothing_is_appended()
    {
        var recipe = RegistryData.Recipe("workflow.step");

        var registered = await Events.Registered(recipe, TestContext.Current.CancellationToken);
        var approved = await Events.Approved(RegistryData.Approval(recipe), drift: false, TestContext.Current.CancellationToken);
        var revoked = await Events.Revoked(RegistryData.Revocation(recipe), TestContext.Current.CancellationToken);

        Assert.All([registered.Error, approved.Error, revoked.Error], e => Assert.Equal("SND-LEDGER-002", e.Code));
        Assert.Empty(await Recorded(EventTypes.RecipeRegistered));
    }

    private async Task<IReadOnlyList<LedgerEvent>> Recorded(string type) =>
        [.. (await _h.Store.ReadAfter(0, 100, TestContext.Current.CancellationToken)).Value.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal))];

    private static RecipePins Pins(string seed) =>
        new([new BlockPin(BlockId.From("gate." + seed), RegistryData.Descriptor(seed))], []);
}
