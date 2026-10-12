using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Workflow.Capabilities;

namespace Aw.Sindri.Tests.Registry;

/// <summary>
/// The pins (spec §2 descriptor hash, §4.1, §4.2 pins digest, §4.3 RCP-CAP-003, W11): the descriptor hash, what an
/// approval pins, the digest a drift challenge binds, and whether an approval still holds against this build.
/// </summary>
public sealed class PinRulesTests
{
    private static readonly BlockDescriptor _produce = TestBlocks.Step("step.produce");

    [Fact, Trait("Lane", "unit")]
    public void The_descriptor_hash_is_sha256_over_the_canonical_json_of_the_descriptor()
    {
        var canonical = CanonicalJson.Of(JsonNode.Parse(AwJson.Serialize(_produce, SliceHarness.Json)));

        var hash = PinRules.Hash(_produce, SliceHarness.Json);

        Assert.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(canonical)), hash.Value);
        Assert.Equal(hash, PinRules.Hash(_produce with { Outcomes = [.. _produce.Outcomes] }, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_version_bump_or_a_contract_change_changes_the_descriptor_hash()
    {
        var hash = PinRules.Hash(_produce, SliceHarness.Json);

        Assert.NotEqual(hash, PinRules.Hash(_produce with { Version = _produce.Version + 1 }, SliceHarness.Json));
        Assert.NotEqual(hash, PinRules.Hash(_produce with { Capabilities = Capability.Network }, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_block_pins_name_every_block_the_recipe_uses_once_sorted_by_id()
    {
        var pins = PinRules.Blocks(CapabilityRecipe.Generated, CapabilityRecipe.Catalog, SliceHarness.Json);

        Assert.Equal(
            ["effect.repo.push", "evidence.tests", "gate.check", "gate.review", "step.produce"],
            pins.Select(p => p.Block.Value),
            StringComparer.Ordinal);
        Assert.All(pins, p => Assert.Equal(PinRules.Hash(CapabilityRecipe.Catalog.Find(p.Block)!, SliceHarness.Json), p.Hash));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_block_this_build_does_not_have_gets_no_pin()
    {
        var catalog = new BlockCatalog([.. CapabilityRecipe.Catalog.Blocks.Where(b => b.Id.Value != "gate.review")], []);

        var pins = PinRules.Blocks(CapabilityRecipe.Generated, catalog, SliceHarness.Json);

        Assert.DoesNotContain(pins, p => p.Block.Value == "gate.review");
        Assert.Equal(4, pins.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_is_pinned_at_its_latest_approved_version()
    {
        var one = RegistryData.Recipe("workflow.step", 1);
        var two = RegistryData.Recipe("workflow.step", 2);
        var other = RegistryData.Recipe("workflow.other", 9);

        var pins = PinRules.Children(CapabilityRecipe.Generated, [RegistryData.Approval(two), RegistryData.Approval(other), RegistryData.Approval(one)]);

        Assert.Equal([new ChildPin(RecipeId.From("workflow.step"), two.Hash)], pins);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_with_no_current_approval_has_no_pin()
    {
        Assert.Empty(PinRules.Children(CapabilityRecipe.Generated, []));
        Assert.Empty(PinRules.Children(ARecipe.Minimal(RecipeId.From("workflow.plain")), [RegistryData.Approval(RegistryData.Recipe("workflow.step"))]));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pins_are_the_block_pins_and_the_child_pins()
    {
        var child = RegistryData.Recipe("workflow.step", 3);

        var pins = PinRules.Pins(CapabilityRecipe.Generated, CapabilityRecipe.Catalog, [RegistryData.Approval(child)], SliceHarness.Json);

        Assert.Equal(PinRules.Blocks(CapabilityRecipe.Generated, CapabilityRecipe.Catalog, SliceHarness.Json), pins.Blocks);
        Assert.Equal([new ChildPin(child.Id, child.Hash)], pins.Children);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pins_digest_is_sha256_over_the_canonical_json_of_the_pins()
    {
        var pins = Pins("a", "b");
        var canonical = CanonicalJson.Of(JsonNode.Parse(AwJson.Serialize(pins, SliceHarness.Json)));

        var digest = PinRules.Digest(pins, SliceHarness.Json);

        Assert.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(canonical)), digest);
        Assert.Equal(digest, PinRules.Digest(Pins("a", "b"), SliceHarness.Json));
        Assert.NotEqual(digest, PinRules.Digest(Pins("a", "c"), SliceHarness.Json), StringComparer.Ordinal);
        Assert.NotEqual(digest, PinRules.Digest(Pins("a", "b") with { Children = [new(RecipeId.From("workflow.step"), RegistryData.Hash("child"))] }, SliceHarness.Json), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_that_was_never_approved_has_no_check() =>
        Assert.Null(PinRules.Check(approval: null, revocation: null, Pins("a").Blocks));

    [Fact, Trait("Lane", "unit")]
    public void An_approval_holds_while_its_block_pins_equal_the_build()
    {
        var recipe = RegistryData.Recipe("workflow.step");
        var approval = RegistryData.Approval(recipe, Pins("a", "b"));

        Assert.Equal(ApprovalCheck.Holds, PinRules.Check(approval, revocation: null, Pins("a", "b").Blocks));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_changed_added_or_removed_block_is_drift()
    {
        var approval = RegistryData.Approval(RegistryData.Recipe("workflow.step"), Pins("a", "b"));

        Assert.Equal(ApprovalCheck.Drifted, PinRules.Check(approval, revocation: null, Pins("a", "c").Blocks));
        Assert.Equal(ApprovalCheck.Drifted, PinRules.Check(approval, revocation: null, Pins("a").Blocks));
        Assert.Equal(ApprovalCheck.Drifted, PinRules.Check(approval, revocation: null, Pins("a", "b", "d").Blocks));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_revoked_hash_is_revoked_even_when_its_pins_match()
    {
        var recipe = RegistryData.Recipe("workflow.step");
        var approval = RegistryData.Approval(recipe, Pins("a"));

        Assert.Equal(ApprovalCheck.Revoked, PinRules.Check(approval, RegistryData.Revocation(recipe), Pins("a").Blocks));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_changed_child_is_never_drift()
    {
        // A newer child version only marks the parent child-stale (spec §4.1); the check compares block pins alone.
        var approval = RegistryData.Approval(RegistryData.Recipe("workflow.item"), Pins("a") with
        {
            Children = [new(RecipeId.From("workflow.step"), RegistryData.Hash("old child"))],
        });

        Assert.Equal(ApprovalCheck.Holds, PinRules.Check(approval, revocation: null, Pins("a").Blocks));
    }

    /// <summary>Block pins for gate.&lt;seed&gt;, each hashed from its seed, sorted by id; no child pins.</summary>
    private static RecipePins Pins(params string[] seeds) =>
        new([.. seeds.Order(StringComparer.Ordinal).Select(s => new BlockPin(BlockId.From("gate." + s), RegistryData.Descriptor(s)))], []);
}
