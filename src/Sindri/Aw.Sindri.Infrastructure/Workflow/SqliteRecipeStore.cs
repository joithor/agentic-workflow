using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The registered recipes in ledger.db's 'recipes' table (engine spec §4.1). Red stub.</summary>
internal sealed class SqliteRecipeStore : IRecipeStore
{
    public SqliteRecipeStore(LedgerDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
    }

    public ValueTask<Result<RecipeRegistration>> Add(RegisteredRecipe recipe, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<RegisteredRecipe?>> Find(RecipeHash hash, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> Versions(RecipeId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> All(CancellationToken cancellationToken) => throw new NotSupportedException();
}
