using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// The registered recipes (spec §4.1, §7.1): the normalized text by hash. Registering the same hash twice is a no-op;
/// the same id and version with another hash is refused with SND-RECIPE-007.
/// </summary>
public interface IRecipeStore
{
    /// <summary>Stores <paramref name="recipe"/> unless its hash is already registered.</summary>
    /// <param name="recipe">The recipe to register.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored row and whether it was added, or SND-RECIPE-007 when its id and version are registered with another hash.</returns>
    public ValueTask<Result<RecipeRegistration>> Add(RegisteredRecipe recipe, CancellationToken cancellationToken);

    /// <summary>The recipe registered under <paramref name="hash"/>.</summary>
    /// <param name="hash">The full hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recipe, or null when that hash isn't registered.</returns>
    public ValueTask<Result<RegisteredRecipe?>> Find(RecipeHash hash, CancellationToken cancellationToken);

    /// <summary>Every registered version of <paramref name="id"/>, lowest version first.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The versions (empty when none is registered).</returns>
    public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> Versions(RecipeId id, CancellationToken cancellationToken);

    /// <summary>Every registered recipe, by id and then version.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recipes (empty when none is registered).</returns>
    public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> All(CancellationToken cancellationToken);
}
