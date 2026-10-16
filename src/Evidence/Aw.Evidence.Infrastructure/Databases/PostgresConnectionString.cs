using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>The connection string for a Postgres entry: every field set explicitly, so no PG environment variable or password file applies.</summary>
internal static class PostgresConnectionString
{
    /// <summary>Builds the connection string.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="password">The role's password.</param>
    /// <returns>The connection string.</returns>
    internal static string Build(DatabaseEntry entry, string password) =>
        throw new NotSupportedException("PostgresConnectionString is not written yet");
}
