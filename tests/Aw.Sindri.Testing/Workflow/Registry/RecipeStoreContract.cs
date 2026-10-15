using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Xunit;

namespace Aw.Sindri.Testing.Workflow.Registry;

/// <summary>
/// The contract every <see cref="IRecipeStore"/> honours (engine spec §4.1): a recipe is kept by hash, a second register
/// of the same hash adds nothing, the same id and version with another hash is SND-RECIPE-007, and listings are ordered.
/// Run against the fake and the SQLite adapter (P2 unit (a)).
/// </summary>
public abstract class RecipeStoreContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IRecipeStore Create();

    /// <summary>A registered recipe is found by its hash with its text.</summary>
    [Fact]
    public async Task Registered_recipe_is_found_by_hash()
    {
        var store = Create();
        var recipe = RegistryData.Recipe("workflow.step");

        Expect.True((await store.Add(recipe, Token)).Value.Added);
        Expect.Equal(recipe, (await store.Find(recipe.Hash, Token)).Value);
    }

    /// <summary>An unknown hash finds nothing.</summary>
    [Fact]
    public async Task Unknown_hash_is_null()
    {
        var found = await Create().Find(RegistryData.Hash("nothing"), Token);

        Expect.True(found.IsSuccess);
        Expect.True(found.Value is null);
    }

    /// <summary>Registering the same hash twice keeps one row and says it added nothing.</summary>
    [Fact]
    public async Task Same_hash_twice_keeps_one_row()
    {
        var store = Create();
        var recipe = RegistryData.Recipe("workflow.step");
        _ = await store.Add(recipe, Token);

        var again = await store.Add(recipe, Token);

        Expect.True(!again.Value.Added);
        Expect.Equal(1, (await store.All(Token)).Value.Count);
    }

    /// <summary>The same id and version with other content is refused with SND-RECIPE-007.</summary>
    [Fact]
    public async Task Same_id_and_version_with_other_content_is_refused()
    {
        var store = Create();
        _ = await store.Add(RegistryData.Recipe("workflow.step", 1, "a"), Token);

        var other = await store.Add(RegistryData.Recipe("workflow.step", 1, "b"), Token);

        Expect.Equal(WorkflowErrors.RecipeVersionTaken.Code, other.Error.Code);
    }

    /// <summary>Versions lists one id's versions, lowest first; All lists by id, then version.</summary>
    [Fact]
    public async Task Listings_are_ordered_by_id_then_version()
    {
        var store = Create();
        foreach (var recipe in new[] { RegistryData.Recipe("workflow.step", 2), RegistryData.Recipe("workflow.item", 1), RegistryData.Recipe("workflow.step", 1) })
        {
            _ = await store.Add(recipe, Token);
        }

        Expect.Sequence([1, 2], (await store.Versions(RecipeId.From("workflow.step"), Token)).Value.Select(r => r.Version));
        Expect.Sequence(["workflow.item@1", "workflow.step@1", "workflow.step@2"],
            (await store.All(Token)).Value.Select(r => $"{r.Id.Value}@{r.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
    }
}
