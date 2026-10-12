using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>
/// context.db: its connections, its migrations and its permissions (spec §3: directory 0700; the file and its -wal and
/// -shm 0600). <see cref="Open"/> migrates once per process; a missing file is a store with no map, never created by a read.
/// </summary>
internal sealed class GraphDatabase : IDisposable
{
    private readonly ContextSettings _settings;
    private readonly MigrationRunner _runner;
    private readonly SemaphoreSlim _migrate = new(1, 1);
    private bool _migrated;

    public GraphDatabase(ContextSettings settings, TimeProvider clock)
    {
        _settings = settings;
        var connections = new SqliteConnectionFactory(new SqliteOptions(settings.DatabasePath));
        Sessions = new SqliteUnitOfWorkFactory(connections);
        _runner = new MigrationRunner(connections, clock);
    }

    public SqliteUnitOfWorkFactory Sessions { get; }

    /// <summary>Whether the file exists yet.</summary>
    public bool Exists => File.Exists(_settings.DatabasePath);

    public static IReadOnlyList<Migration> Migrations { get; } =
        EmbeddedMigrations.Load(typeof(GraphDatabase).Assembly, "Aw.Context.Infrastructure.Graph.Migrations.").Value;

    public void Dispose() => _migrate.Dispose();

    /// <summary>Migrates the file once per process (creating it), then returns the session factory.</summary>
    public async ValueTask<Result<SqliteUnitOfWorkFactory>> Open(CancellationToken cancellationToken)
    {
        if (_migrated)
        {
            return Sessions;
        }

        await _migrate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_migrated)
            {
                OwnerOnly.EnsureDirectory(Path.GetDirectoryName(_settings.StateDirectory)!);   // why: the state directory is an absolute path
                OwnerOnly.EnsureDirectory(_settings.StateDirectory);
                var report = await _runner.Migrate(Migrations, cancellationToken).ConfigureAwait(false);
                if (report.IsError)
                {
                    return Translate(report.Error);
                }

                var db = _settings.DatabasePath;
                OwnerOnly.RestrictFiles(db, $"{db}-wal", $"{db}-shm");
                _migrated = true;
            }

            return Sessions;
        }
        finally
        {
            _ = _migrate.Release();
        }
    }

    /// <summary>A store failure as SND-CONTEXT-004, keeping the Kernel.Data code in the message.</summary>
    public static Error Translate(Error error) =>
        ContextErrors.StoreUnavailable.With($"{error.Code}: {error.Message} (fix: check $AW_SINDRI_HOME permissions; never delete context.db, it holds review decisions)");
}
