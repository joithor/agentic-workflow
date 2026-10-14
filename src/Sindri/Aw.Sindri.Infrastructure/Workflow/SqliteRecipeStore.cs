using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The registered recipes in ledger.db's 'recipes' table (engine spec §4.1): the normalized text by hash, one hash per
/// id and version. A register checks and inserts inside one write transaction.
/// </summary>
internal sealed class SqliteRecipeStore(LedgerDatabase database) : IRecipeStore
{
    public ValueTask<Result<RecipeRegistration>> Add(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return RecipeSessions.Write(database, async ValueTask<Result<RecipeRegistration>> (session, token) =>
        {
            var existing = await RecipeSessions.First(session, ByHash(recipe.Hash), RegisteredRecipeMapper.Instance, token).ConfigureAwait(false);
            if (existing.IsError)
            {
                return existing.Error;
            }

            if (existing.Value is { } stored)
            {
                return new RecipeRegistration(stored, Added: false);
            }

            var taken = await RecipeSessions.First(session, ByKey(recipe.Id, recipe.Version), RegisteredRecipeMapper.Instance, token).ConfigureAwait(false);
            if (taken.IsError)
            {
                return taken.Error;
            }

            if (taken.Value is { } other)
            {
                return WorkflowErrors.RecipeVersionTaken.With(
                    $"{recipe.Id.Value} version {recipe.Version.ToString(CultureInfo.InvariantCulture)} is registered as {other.Hash.Display}; raise 'version'");
            }

            var inserted = await RecipeSessions.Execute(session, Insert(recipe), token).ConfigureAwait(false);
            return inserted.IsError ? inserted.Error : new RecipeRegistration(recipe, Added: true);
        }, cancellationToken);
    }

    public ValueTask<Result<RegisteredRecipe?>> Find(RecipeHash hash, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, empty: null,
            (session, token) => RecipeSessions.First(session, ByHash(hash), RegisteredRecipeMapper.Instance, token), cancellationToken);

    public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> Versions(RecipeId id, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(RecipeSql.SelectVersions, c => c.Bind("$id", id.Value)), RegisteredRecipeMapper.Instance, token),
            cancellationToken);

    public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> All(CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(RecipeSql.SelectAllRecipes), RegisteredRecipeMapper.Instance, token),
            cancellationToken);

    private static SqlStatement ByHash(RecipeHash hash) => new(RecipeSql.SelectRecipeByHash, c => c.Bind("$hash", hash.Value));

    private static SqlStatement ByKey(RecipeId id, int version) =>
        new(RecipeSql.SelectRecipeByKey, c => c.Bind("$id", id.Value).Bind("$version", version));

    private static SqlStatement Insert(RegisteredRecipe recipe) => new(RecipeSql.InsertRecipe, c => c
        .Bind("$hash", recipe.Hash.Value)
        .Bind("$id", recipe.Id.Value)
        .Bind("$version", recipe.Version)
        .Bind("$text", recipe.Text)
        .Bind("$baseId", recipe.Base?.Id.Value)
        .Bind("$baseHash", recipe.Base?.Hash.Value)
        .Bind("$registeredAt", recipe.RegisteredAt), $"recipe {recipe.Id.Value}@{recipe.Hash.Display}");
}
