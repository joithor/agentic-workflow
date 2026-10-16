using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>Reads a Postgres database through Npgsql, as a role that can write nothing, in read-only transactions.</summary>
internal sealed class PostgresDatabaseReader : IDatabaseReader
{
    /// <inheritdoc/>
    public ValueTask<Result<IReadSession>> OpenAsync(DbTarget target, CancellationToken cancellationToken) =>
        target is PostgresTarget postgres
            ? throw new NotSupportedException("PostgresDatabaseReader is not written yet: " + PostgresConnectionString.Build(postgres.Entry, postgres.Password))
            : throw new NotSupportedException("PostgresDatabaseReader is not written yet");
}
