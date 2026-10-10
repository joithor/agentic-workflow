using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

/// <summary>A session on an open connection with no transaction: each statement commits on its own.</summary>
public sealed class ConnectionSession(SqliteConnection connection) : ISqliteSession, IAsyncDisposable
{
    public SqliteConnection Connection { get; } = connection;

    public SqliteTransaction? Transaction => null;

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
