using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// The pins (spec §2 descriptor hash, §4.1, §4.3 RCP-CAP-003, W11): what an approval pins, the digest a drift
/// challenge binds, and whether an approval still holds against this build. Pure. P2 contract: stubs, owned by unit (d).
/// </summary>
public static class PinRules
{
    /// <summary>The descriptor hash of <paramref name="descriptor"/>: SHA-256 over its RFC 8785 canonical JSON.</summary>
    /// <param name="descriptor">The block's descriptor.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The hash.</returns>
    public static DescriptorHash Hash(BlockDescriptor descriptor, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        throw new NotSupportedException(Pending);
    }

    /// <summary>The block pins of <paramref name="recipe"/>: each block it names with its descriptor hash in <paramref name="catalog"/>, sorted by id.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">This build's block catalog.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The pins.</returns>
    public static IReadOnlyList<BlockPin> Blocks(Recipe recipe, BlockCatalog catalog, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        throw new NotSupportedException(Pending);
    }

    /// <summary>
    /// The child pins of <paramref name="recipe"/> (spec §4.1): each child recipe it waits on, with the hash of that
    /// child's latest approved version in <paramref name="current"/>, sorted by id. A child with no current approval has
    /// no pin.
    /// </summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="current">The latest approval of every key that isn't revoked (IRecipeApprovals.Current).</param>
    /// <returns>The pins.</returns>
    public static IReadOnlyList<ChildPin> Children(Recipe recipe, IEnumerable<RecipeApproval> current)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(current);
        throw new NotSupportedException(Pending);
    }

    /// <summary>What an approval of <paramref name="recipe"/> pins against this build: its block pins and its child pins.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">This build's block catalog.</param>
    /// <param name="current">The latest approval of every key that isn't revoked.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The pins.</returns>
    public static RecipePins Pins(Recipe recipe, BlockCatalog catalog, IEnumerable<RecipeApproval> current, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(options);
        throw new NotSupportedException(Pending);
    }

    /// <summary>The digest a drift approval's challenge binds (spec §4.2): SHA-256 over the canonical JSON of <paramref name="pins"/>.</summary>
    /// <param name="pins">The pins the approval will record.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>'sha256:' and 64 hex digits.</returns>
    public static string Digest(RecipePins pins, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(options);
        throw new NotSupportedException(Pending);
    }

    /// <summary>RCP-CAP-003: the approval holds when it exists, isn't revoked, and its block pins equal <paramref name="build"/>.</summary>
    /// <param name="approval">The latest approval of the key, or null.</param>
    /// <param name="revocation">The hash's revocation, or null.</param>
    /// <param name="build">This build's block pins for the recipe.</param>
    /// <returns>Holds, Revoked or Drifted; null when it was never approved (SND-RECIPE-003).</returns>
    public static ApprovalCheck? Check(RecipeApproval? approval, RecipeRevocation? revocation, IReadOnlyList<BlockPin> build)
    {
        _ = approval;
        _ = revocation;
        ArgumentNullException.ThrowIfNull(build);
        throw new NotSupportedException(Pending);
    }

    private const string Pending = "PinRules arrives with engine P2 unit (d)";
}
