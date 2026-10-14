using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Testing.Workflow.Registry;

/// <summary>Registry rows for the contract bases and slice tests: stable hashes from a seed, at <see cref="KnownInstants.Epoch"/>.</summary>
public static class RegistryData
{
    /// <summary>A recipe hash derived from <paramref name="seed"/>.</summary>
    /// <param name="seed">Any text.</param>
    /// <returns>The hash.</returns>
    public static RecipeHash Hash(string seed) => RecipeHash.Of(Encoding.UTF8.GetBytes(seed));

    /// <summary>A descriptor hash derived from <paramref name="seed"/>.</summary>
    /// <param name="seed">Any text.</param>
    /// <returns>The hash.</returns>
    public static DescriptorHash Descriptor(string seed) =>
        DescriptorHash.From("sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seed))));

    /// <summary>A registered recipe '<paramref name="id"/>' at <paramref name="version"/>, its hash seeded by its id, version and <paramref name="content"/>.</summary>
    /// <param name="id">The recipe id, for example 'workflow.step'.</param>
    /// <param name="version">Its version.</param>
    /// <param name="content">Distinguishes two contents of the same id and version.</param>
    /// <param name="baseRecipe">The base of a generated recipe, or null.</param>
    /// <returns>The row.</returns>
    public static RegisteredRecipe Recipe(string id, int version = 1, string content = "a", RecipeRef? baseRecipe = null)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{id}@{version}:{content}");
        return new RegisteredRecipe(RecipeId.From(id), version, Hash(text), text, baseRecipe, KnownInstants.Epoch);
    }

    /// <summary>A desk approval of <paramref name="recipe"/> by 'human:cli' with <paramref name="pins"/> (none when null).</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="pins">Its pins.</param>
    /// <returns>The approval.</returns>
    public static RecipeApproval Approval(RegisteredRecipe recipe, RecipePins? pins = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new RecipeApproval(recipe.Id, recipe.Version, recipe.Hash, "human:cli", KnownInstants.Epoch,
            recipe.Base is null ? ApprovalVia.Desk : ApprovalVia.Freeze, recipe.Base, pins ?? RecipePins.None);
    }

    /// <summary>A revoke of <paramref name="recipe"/> by 'human:cli'.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="cascadedFrom">The base whose revoke cascaded, or null.</param>
    /// <returns>The revocation.</returns>
    public static RecipeRevocation Revocation(RegisteredRecipe recipe, RecipeHash? cascadedFrom = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new RecipeRevocation(recipe.Id, recipe.Version, recipe.Hash, "human:cli", KnownInstants.Epoch, cascadedFrom);
    }
}
