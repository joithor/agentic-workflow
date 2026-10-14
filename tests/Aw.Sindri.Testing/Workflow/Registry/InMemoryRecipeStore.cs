using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Testing.Workflow.Registry;

/// <summary>The registered recipes in memory (<see cref="RecipeStoreContract"/> keeps it honest).</summary>
public sealed class InMemoryRecipeStore : IRecipeStore
{
    private readonly List<RegisteredRecipe> _recipes = [];
    private readonly Lock _gate = new();

    /// <inheritdoc/>
    public ValueTask<Result<RecipeRegistration>> Add(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        lock (_gate)
        {
            if (_recipes.Find(r => r.Hash == recipe.Hash) is { } existing)
            {
                return Done<RecipeRegistration>(new RecipeRegistration(existing, Added: false));
            }

            if (_recipes.Find(r => r.Id == recipe.Id && r.Version == recipe.Version) is { } taken)
            {
                return Done<RecipeRegistration>(WorkflowErrors.RecipeVersionTaken.With(
                    $"{recipe.Id.Value} version {recipe.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)} is registered as {taken.Hash.Display}; raise 'version'"));
            }

            _recipes.Add(recipe);
            return Done<RecipeRegistration>(new RecipeRegistration(recipe, Added: true));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<RegisteredRecipe?>> Find(RecipeHash hash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done<RegisteredRecipe?>(_recipes.Find(r => r.Hash == hash));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> Versions(RecipeId id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done(Results.Success<IReadOnlyList<RegisteredRecipe>>([.. _recipes.Where(r => r.Id == id).OrderBy(r => r.Version)]));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> All(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done(Results.Success<IReadOnlyList<RegisteredRecipe>>([.. _recipes.OrderBy(r => r.Id.Value, StringComparer.Ordinal).ThenBy(r => r.Version)]));
        }
    }

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);
}
