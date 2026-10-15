using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a 'recipe_approvals' row as an approval (<see cref="RecipeSql"/> column order).</summary>
internal sealed class RecipeApprovalMapper : IRowMapper<RecipeApproval>
{
    public static readonly RecipeApprovalMapper Instance = new();

    public RecipeApproval Map(SqliteDataReader reader) => new(
        RecipeId.From(reader.GetString(0)),
        reader.GetInt32(1),
        RecipeHash.From(reader.GetString(2)),
        reader.GetString(3),
        SqliteRead.GetTimestamp(reader, 4),
        RecipeRows.Via(reader.GetString(5)),
        RecipeRows.Base(reader, 6),
        new RecipePins(RecipeRows.BlockPins(reader.GetString(8)), RecipeRows.ChildPins(reader.GetString(9))));
}
