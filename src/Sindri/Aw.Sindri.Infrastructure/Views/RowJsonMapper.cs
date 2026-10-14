using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>Maps a view row's JSON text.</summary>
internal sealed class RowJsonMapper : IRowMapper<string>
{
    public static readonly RowJsonMapper Instance = new();

    public string Map(SqliteDataReader reader) => reader.GetString(0);
}
