using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The recipe stats cache over views.db (API spec §3.8, P12). A read takes the ordinary read side; the one write is its own short transaction (the projector
/// writes pages in others), kept only while the projector's cursor hasn't passed the ledger head the stats were counted at. It never creates views.db: the
/// projector does, and a cache with no file has nothing to keep.
/// </summary>
internal sealed class SqliteRecipeStatsCache(ViewsDatabase database, IViewReader reader, JsonSerializerOptions options) : IRecipeStatsCache
{
    public async ValueTask<Result<RecipeStats?>> Get(RecipeId recipe, string? hash, StatsWindow window, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        var key = RecipeStatsKey.Of(recipe.Value, hash, window.Text);
        var row = await reader.Get<RecipeStatsRow>(ViewNames.RecipeStats, key, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        if (row.Value is not { } found)
        {
            return Results.Success<RecipeStats?>(value: null);
        }

        var age = now - found.CachedAt;
        return Results.Success(age >= TimeSpan.Zero && age < IRecipeStatsCache.MaxAge ? found.Stats : null);
    }

    public async ValueTask<Result<Unit>> Put(RecipeStats stats, long head, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stats);
        if (!database.Exists)
        {
            return Unit.Value;
        }

        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ViewsDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var text = Encoding.UTF8.GetString(AwJson.Serialize(new RecipeStatsRow(stats, now), options));
            var key = RecipeStatsKey.Of(stats.Recipe, stats.Hash, stats.Window);
            var put = await SqliteQueries.Execute(begun.Value, new SqlStatement(ViewSql.PutRecipeStats, c => c.Bind("$key", key).Bind("$row", text).Bind("$head", head)),
                cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                return ViewsDatabase.Translate(put.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ViewsDatabase.Translate(committed.Error) : Unit.Value;
        }
    }
}
