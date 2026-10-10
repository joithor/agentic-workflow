using Aw.Kernel.Outbox;
using Aw.Kernel.Persistence;
using Aw.Kernel.Testing;

namespace Aw.Kernel.Data.Tests;

/// <summary>The shared outbox contract bound to SqliteOutbox on a temp file (integration lane).</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteOutboxContractTests : OutboxContract, IAsyncDisposable
{
    private readonly TempDatabase _db = new();
    private ConnectionSession? _session;

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
        }

        _db.Dispose();
    }

    protected override async ValueTask<IOutbox> CreateOutbox(TimeProvider clock, OutboxRetryPolicy policy, CancellationToken cancellationToken)
    {
        var migrated = await new MigrationRunner(_db.Factory(), clock).Migrate([SqliteOutboxSchema.CreateMigration(1)], cancellationToken);
        Assert.True(migrated.IsSuccess);
        _session = new ConnectionSession((await _db.Factory().Open(cancellationToken)).Value);
        return new SqliteOutbox(_session, clock, policy);
    }

    protected override ValueTask<IUnitOfWorkFactory> CreateUnitOfWorkFactory(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IUnitOfWorkFactory>(new SqliteUnitOfWorkFactory(_db.Factory()));

    protected override IOutbox OutboxIn(IUnitOfWork work, TimeProvider clock, OutboxRetryPolicy policy) =>
        new SqliteOutbox((SqliteUnitOfWork)work, clock, policy);
}
