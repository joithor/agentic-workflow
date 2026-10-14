using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a 'recipes' row (<see cref="RecipeSql"/> column order).</summary>
internal sealed class RegisteredRecipeMapper : IRowMapper<RegisteredRecipe>
{
    public static readonly RegisteredRecipeMapper Instance = new();

    public RegisteredRecipe Map(SqliteDataReader reader) => new(
        RecipeId.From(reader.GetString(0)),
        reader.GetInt32(1),
        RecipeHash.From(reader.GetString(2)),
        reader.GetString(3),
        RecipeRows.Base(reader, 4),
        SqliteRead.GetTimestamp(reader, 6));
}
