using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>Maps a single text column, null when it is.</summary>
internal sealed class TextMapper : IRowMapper<string?>
{
    public static readonly TextMapper Instance = new();

    public string? Map(SqliteDataReader reader) => reader.IsDBNull(0) ? null : reader.GetString(0);
}
