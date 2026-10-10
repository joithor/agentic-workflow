using Aw.Kernel.Persistence;
using Aw.Kernel.Testing;

namespace Aw.Kernel.Data.Tests;

/// <summary>The shared unit-of-work contract bound to SqliteUnitOfWorkFactory on a temp file (integration lane).</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteUnitOfWorkContractTests : UnitOfWorkContract, IDisposable
{
    private static readonly Migration _records = new(1, "records", "CREATE TABLE records (key INTEGER PRIMARY KEY);");
    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    protected override async ValueTask<IUnitOfWorkFactory> CreateFactory(CancellationToken cancellationToken)
    {
        var migrated = await new MigrationRunner(_db.Factory(), new FixedClock(DateTimeOffset.UnixEpoch)).Migrate([_records], cancellationToken);
        Assert.True(migrated.IsSuccess);
        return new SqliteUnitOfWorkFactory(_db.Factory());
    }

    protected override async ValueTask Write(IUnitOfWork work, int key, CancellationToken cancellationToken)
    {
        var written = await SqliteQueries.Execute(
            (SqliteUnitOfWork)work, new SqlStatement("INSERT INTO records (key) VALUES ($key)", c => c.Bind("$key", key)), cancellationToken);
        Assert.True(written.IsSuccess);
    }

    protected override async ValueTask<bool> IsCommitted(int key, CancellationToken cancellationToken)
    {
        await using var read = (await new SqliteUnitOfWorkFactory(_db.Factory()).BeginRead(cancellationToken)).Value;
        return (await SqliteQueries.Exists(read, new SqlStatement("SELECT EXISTS (SELECT 1 FROM records WHERE key = $key)", c => c.Bind("$key", key)), cancellationToken)).Value;
    }
}
