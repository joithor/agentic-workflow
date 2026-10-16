using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>Reads a SQLite file through a read-only copy of it in the run directory.</summary>
internal sealed class SqliteDatabaseReader : IDatabaseReader
{
    /// <inheritdoc/>
    public ValueTask<Result<IReadSession>> OpenAsync(DbTarget target, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SqliteDatabaseReader is not written yet");
}
