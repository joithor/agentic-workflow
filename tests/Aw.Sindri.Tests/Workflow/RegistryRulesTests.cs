using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Workflow.Capabilities;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The rules that read the registry (spec §3.2 'version', §4.3, §9.1, W9): RCP-VERSION-001 (the same id and version is
/// registered with another hash), RCP-VERSION-002 (a new version isn't above the highest registered) and RCP-CAP-003 (a
/// generated recipe's base is registered, approved, not revoked, and its pins match the build). Each runs only when the
/// context carries the registry.
/// </summary>
public sealed class RegistryRulesTests
{
    private static readonly RecipeHash _mine = RegistryData.Hash("mine");
    private static readonly RecipeHash _registered = RegistryData.Hash("registered");

    [Fact, Trait("Lane", "unit")]
    public void Same_id_and_version_registered_with_another_hash_is_refused()
    {
        // The recipe of invalid/RCP-VERSION-001.yaml; 'version' is at 3:1.
        var registry = View(("workflow.version-taken", 1, _registered));

        Assert.Equal(
            ["RCP-VERSION-001: workflow.version-taken version 1 is registered with hash b1a9e561106e, but this recipe's hash is 3fd30542fe3f at RCP-VERSION-001.yaml:3:1 (version); raise version to 2, one above the highest registered, or restore the content registered as version 1"],
            Lines(Recipe("workflow.version-taken", 1), registry, file: "RCP-VERSION-001.yaml", positions: [("version", 3, 1)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_fix_names_one_above_the_highest_registered_version()
    {
        var registry = View(("workflow.version-taken", 1, _registered), ("workflow.version-taken", 4, RegistryData.Hash("four")));

        Assert.Equal(
            ["RCP-VERSION-001: workflow.version-taken version 1 is registered with hash b1a9e561106e, but this recipe's hash is 3fd30542fe3f at r.yaml:1:1 (version); raise version to 5, one above the highest registered, or restore the content registered as version 1"],
            Lines(Recipe("workflow.version-taken", 1), registry));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_version_below_the_highest_registered_is_refused()
    {
        // The recipe of invalid/RCP-VERSION-002.yaml; 'version' is at 3:1.
        var registry = View(("workflow.version-behind", 1, _registered), ("workflow.version-behind", 3, RegistryData.Hash("three")));

        Assert.Equal(
            ["RCP-VERSION-002: workflow.version-behind version 2 isn't registered and is below version 3, the highest registered at RCP-VERSION-002.yaml:3:1 (version); raise version to 4, one above the highest registered"],
            Lines(Recipe("workflow.version-behind", 2), registry, file: "RCP-VERSION-002.yaml", positions: [("version", 3, 1)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_registered_hash_itself_and_a_new_version_above_the_highest_pass()
    {
        var registry = View(("workflow.versioned", 1, RegistryData.Hash("one")), ("workflow.versioned", 2, _mine));

        Assert.Empty(Lines(Recipe("workflow.versioned", 2), registry));
        Assert.Empty(Lines(Recipe("workflow.versioned", 3), registry));
        Assert.Empty(Lines(Recipe("workflow.versioned", 7), registry));
    }

    [Fact, Trait("Lane", "unit")]
    public void Other_ids_do_not_count()
    {
        var registry = View(("workflow.other", 1, _registered), ("workflow.other", 5, RegistryData.Hash("five")));

        Assert.Empty(Lines(Recipe("workflow.versioned", 1), registry));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_registry_no_registry_rule_runs()
    {
        Assert.Empty(Lines(Recipe("workflow.version-taken", 1), registry: null));
        Assert.Empty(Lines(CapabilityRecipe.Generated, registry: null, baseRecipe: CapabilityRecipe.Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_base_that_is_registered_but_not_approved_is_refused()
    {
        // The recipe of invalid/RCP-CAP-003.yaml; 'base' is at 4:1.
        var registry = View(("workflow.cap-base", 1, CapabilityRecipe.BaseHash));

        Assert.Equal(
            ["RCP-CAP-003: base workflow.cap-base@16d368e1cdf8 is registered but not approved at RCP-CAP-003.yaml:4:1 (base); approve the base at a terminal (sindri recipe approve workflow.cap-base@16d368e1cdf8), or generate the recipe from an approved base"],
            Lines(CapabilityRecipe.Generated, registry, CapabilityRecipe.Base, "RCP-CAP-003.yaml", [("base", 4, 1)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_base_that_is_not_registered_is_refused()
    {
        Assert.Equal(
            ["RCP-CAP-003: base workflow.cap-base@16d368e1cdf8 isn't registered at r.yaml:1:1 (base); generate the recipe from a registered, approved base"],
            Lines(CapabilityRecipe.Generated, RecipeRegistryView.Empty, CapabilityRecipe.Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_revoked_base_is_refused()
    {
        var registry = View(RegistryApproval.Revoked, ("workflow.cap-base", 1, CapabilityRecipe.BaseHash));

        Assert.Equal(
            ["RCP-CAP-003: base workflow.cap-base@16d368e1cdf8 is revoked at r.yaml:1:1 (base); generate the recipe from an approved base, since a revoked hash can't be approved again"],
            Lines(CapabilityRecipe.Generated, registry, CapabilityRecipe.Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_base_whose_pins_drifted_is_refused()
    {
        var registry = View(RegistryApproval.Drifted, ("workflow.cap-base", 1, CapabilityRecipe.BaseHash));

        Assert.Equal(
            ["RCP-CAP-003: base workflow.cap-base@16d368e1cdf8 was approved against other block descriptors than this build's at r.yaml:1:1 (base); re-approve the base at a terminal (sindri recipe approve workflow.cap-base@16d368e1cdf8), then generate the recipe again"],
            Lines(CapabilityRecipe.Generated, registry, CapabilityRecipe.Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_approved_base_that_holds_passes()
    {
        var registry = View(RegistryApproval.Holds, ("workflow.cap-base", 1, CapabilityRecipe.BaseHash));

        Assert.Empty(Lines(CapabilityRecipe.Generated, registry, CapabilityRecipe.Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hand_written_recipe_has_no_base_to_check() =>
        // RCP-CAP-004 refuses a 'base' on a hand-written recipe (no base in the context); RCP-CAP-003 leaves it to that rule.
        Assert.Empty(Lines(CapabilityRecipe.Generated, RecipeRegistryView.Empty, baseRecipe: null));

    private static Recipe Recipe(string id, int version) => ARecipe.Minimal(RecipeId.From(id)) with { Version = version };

    private static RecipeRegistryView View(params (string Id, int Version, RecipeHash Hash)[] registered) =>
        new([.. registered.Select(r => new RegisteredVersion(RecipeId.From(r.Id), r.Version, r.Hash))], new Dictionary<RecipeHash, RegistryApproval>());

    private static RecipeRegistryView View(RegistryApproval standing, params (string Id, int Version, RecipeHash Hash)[] registered) =>
        View(registered) with { Approvals = registered.ToDictionary(r => r.Hash, _ => standing) };

    private static IReadOnlyList<string> Lines(
        Recipe recipe,
        RecipeRegistryView? registry,
        Recipe? baseRecipe = null,
        string file = "r.yaml",
        IEnumerable<(string Path, int Line, int Column)>? positions = null)
    {
        var source = new RecipeSource(file, new SourcePositions(
            (positions ?? []).ToDictionary(p => p.Path, p => new SourcePosition(p.Line, p.Column), StringComparer.Ordinal)));
        var document = new RecipeDocument(recipe, _mine, source);
        var context = new ValidationContext(CapabilityRecipe.Catalog, RecipeProfileFacts.None, InMemoryChildRecipes.None, baseRecipe) { Registry = registry };
        return [.. new RegistryRules().Check(document, context).Select(f => f.ToLine())];
    }
}
