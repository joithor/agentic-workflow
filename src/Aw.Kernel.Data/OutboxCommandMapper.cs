using Aw.Kernel.Outbox;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Maps an outbox_commands row (SqliteOutbox's column order) to an <see cref="OutboxCommand"/>.</summary>
internal sealed class OutboxCommandMapper : IRowMapper<OutboxCommand>
{
    public static readonly OutboxCommandMapper Instance = new();

    public OutboxCommand Map(SqliteDataReader reader) => new(
        SqliteRead.GetGuid(reader, 0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetInt32(3),
        SqliteRead.GetTimestamp(reader, 4),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetInt32(7),
        SqliteRead.GetGuid(reader, 8),
        reader.IsDBNull(9) ? null : SqliteRead.GetGuid(reader, 9),
        reader.IsDBNull(10) ? null : reader.GetString(10));
}
