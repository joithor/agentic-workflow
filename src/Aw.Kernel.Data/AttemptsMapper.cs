using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Maps the one-column attempts query of SqliteOutbox.</summary>
internal sealed class AttemptsMapper : IRowMapper<int>
{
    public static readonly AttemptsMapper Instance = new();

    public int Map(SqliteDataReader reader) => reader.GetInt32(0);
}
