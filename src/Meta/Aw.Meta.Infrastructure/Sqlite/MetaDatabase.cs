using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>
/// meta.db: creates its directory and applies the embedded migrations on first use (WAL and the busy timeout come from
/// Aw.Kernel.Data's connection factory), then hands out units of work and read sessions. A store that can't be opened is
/// an error result on every call, never an exception.
/// </summary>
/// <param name="options">The database path and busy timeout.</param>
/// <param name="clock">The clock the migration runner stamps schema_version with.</param>
internal sealed class MetaDatabase(SqliteOptions options, TimeProvider clock)
{
    internal const string FileName = "meta.db";
    internal const string ResourcePrefix = "Aw.Meta.Infrastructure.Sqlite.Migrations.";
    internal static readonly TimeSpan BusyTimeout = TimeSpan.FromSeconds(5);

    private readonly SqliteConnectionFactory _connections = new(options);
    private readonly SqliteUnitOfWorkFactory _sessions = new(new SqliteConnectionFactory(options));
    private volatile bool _ready;

    /// <summary>The options for meta.db in the state directory.</summary>
    /// <param name="stateDirectory">$AW_STATE_DIR/meta.</param>
    /// <returns>The options.</returns>
    internal static SqliteOptions OptionsFor(string stateDirectory) =>
        new(Path.Combine(stateDirectory, FileName)) { BusyTimeout = BusyTimeout };

    internal async ValueTask<Result<SqliteUnitOfWork>> BeginWrite(CancellationToken cancellationToken)
    {
        var ready = await Ready(cancellationToken).ConfigureAwait(false);
        return ready.IsError ? ready.Error : await _sessions.BeginSession(cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<Result<SqliteReadSession>> BeginRead(CancellationToken cancellationToken)
    {
        var ready = await Ready(cancellationToken).ConfigureAwait(false);
        return ready.IsError ? ready.Error : await _sessions.BeginRead(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Ready(CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return Results.Ok();
        }

        var directory = MakeDirectory();
        if (directory.IsError)
        {
            return directory.Error;
        }

        var migrations = EmbeddedMigrations.Load(typeof(MetaDatabase).Assembly, ResourcePrefix);
        if (migrations.IsError)
        {
            return migrations.Error;
        }

        var migrated = await new MigrationRunner(_connections, clock).Migrate(migrations.Value, cancellationToken).ConfigureAwait(false);
        if (migrated.IsError)
        {
            return migrated.Error;
        }

        _ready = true;
        return Results.Ok();
    }

    private Result<Unit> MakeDirectory()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath));
        try
        {
            if (directory is not null)
            {
                _ = Directory.CreateDirectory(directory);
            }

            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DataErrors.Unavailable.With($"Can't create the meta state directory '{directory}': {exception.Message}");
        }
    }
}
