using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a revoked 'recipe_approvals' row as its revocation (<see cref="RecipeSql.SelectRevocation"/> column order).</summary>
internal sealed class RecipeRevocationMapper : IRowMapper<RecipeRevocation>
{
    public static readonly RecipeRevocationMapper Instance = new();

    public RecipeRevocation Map(SqliteDataReader reader) => new(
        RecipeId.From(reader.GetString(0)),
        reader.GetInt32(1),
        RecipeHash.From(reader.GetString(2)),
        reader.GetString(3),
        SqliteRead.GetTimestamp(reader, 4),
        reader.IsDBNull(5) ? null : RecipeHash.From(reader.GetString(5)));
}
