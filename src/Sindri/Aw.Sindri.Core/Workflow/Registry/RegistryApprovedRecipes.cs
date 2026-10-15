using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// The approved recipes the interpreter and the start and command slices read (engine spec §4.1, §5.1 'LoadRecipe', §5.2,
/// RCP-CAP-003), over P2's registry: a recipe starts only when it is registered and its latest approval isn't revoked
/// (SND-RECIPE-003 otherwise), and the approval check of a running instance's recipe compares its pins with this build.
/// </summary>
/// <param name="store">The registered recipes.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="catalog">This build's blocks, against which an approval's pins drift.</param>
/// <param name="json">The package's AwJson options (they read the stored text and hash the descriptors).</param>
internal sealed class RegistryApprovedRecipes(IRecipeStore store, IRecipeApprovals approvals, BlockCatalog catalog, JsonSerializerOptions json) : IApprovedRecipes
{
    public async ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken)
    {
        var current = await approvals.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        if (current.Value.Where(a => a.Id == id).MaxBy(static a => a.Version) is { } approved)
        {
            return await Load(id, approved.Hash, cancellationToken).ConfigureAwait(false);
        }

        var versions = await store.Versions(id, cancellationToken).ConfigureAwait(false);
        return versions.IsError ? versions.Error
            : versions.Value.Count == 0 ? WorkflowErrors.RecipeNotFound.With($"no recipe {id.Value} is registered (fix: sindri recipe register <file>, then sindri recipe approve {id.Value})")
            : await NotApproved(id, versions.Value[^1], cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken)
    {
        var found = await store.Find(hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError || found.Value is not { } registered || registered.Id != id)
        {
            return found.IsError ? found.Error : WorkflowErrors.RecipeNotFound.With($"no recipe {id.Value}@{hash.Display} is registered (fix: sindri recipe list)");
        }

        var latest = await approvals.Latest(id, registered.Version, hash, cancellationToken).ConfigureAwait(false);
        return latest.IsError ? latest.Error
            : latest.Value is null ? await NotApproved(id, registered, cancellationToken).ConfigureAwait(false)
            : Parse(registered);
    }

    public async ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var found = await store.Find(recipe.Hash, cancellationToken).ConfigureAwait(false);
        var latest = found.IsError ? found.Error : await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, cancellationToken).ConfigureAwait(false);
        var revoked = latest.IsError ? latest.Error : await approvals.Revocation(recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (revoked.IsError)
        {
            return revoked.Error;
        }

        var parsed = found.Value is { } registered ? Parse(registered) : null;
        if (parsed is { IsError: true })
        {
            return parsed.Error;
        }

        var blocks = parsed is null ? [] : PinRules.Blocks(parsed.Value.Recipe, catalog, json);
        return PinRules.Check(latest.Value, revoked.Value, blocks) is { } check
            ? check
            : WorkflowErrors.RecipeNotApproved.With($"{recipe.Id.Value}@{recipe.Hash.Display} was never approved (fix: sindri recipe approve {recipe.Id.Value} {recipe.Version.ToString(CultureInfo.InvariantCulture)})");
    }

    /// <summary>SND-RECIPE-003, naming whether the version was revoked or never approved.</summary>
    private async ValueTask<Result<ApprovedRecipe>> NotApproved(RecipeId id, RegisteredRecipe registered, CancellationToken cancellationToken)
    {
        var revoked = await approvals.Revocation(registered.Hash, cancellationToken).ConfigureAwait(false);
        if (revoked.IsError)
        {
            return revoked.Error;
        }

        var version = registered.Version.ToString(CultureInfo.InvariantCulture);
        var why = revoked.Value is null ? "isn't approved" : "was revoked, and no other version is approved";
        return WorkflowErrors.RecipeNotApproved.With($"{id.Value} v{version} ({registered.Hash.Display}) {why} (fix: sindri recipe approve {id.Value} {version})");
    }

    private async ValueTask<Result<ApprovedRecipe>> Load(RecipeId id, RecipeHash hash, CancellationToken cancellationToken)
    {
        var found = await store.Find(hash, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error
            : found.Value is { } registered ? Parse(registered)
            : WorkflowErrors.RecipeNotFound.With($"{id.Value}@{hash.Display} is approved but its text isn't registered (fix: sindri recipe register <file>)");
    }

    /// <summary>The registered text read back, carrying its hash.</summary>
    private Result<ApprovedRecipe> Parse(RegisteredRecipe registered)
    {
        var recipe = AwJson.Deserialize<Recipe>(Encoding.UTF8.GetBytes(registered.Text), json);
        return recipe.IsError
            ? recipe.Error
            : new ApprovedRecipe(new RecipeVersion(registered.Id, registered.Version, registered.Hash), recipe.Value with { Hash = registered.Hash });
    }
}
