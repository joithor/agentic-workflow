using Aw.Kernel.Outbox;
using Aw.Kernel.Persistence;

namespace Aw.Kernel.Testing.Tests;

/// <summary>The outbox contract bound to the in-memory fake (unit lane). Aw.Kernel.Data.Tests binds it to SQLite.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryOutboxContractTests : OutboxContract
{
    private InMemoryOutbox? _outbox;

    protected override ValueTask<IOutbox> CreateOutbox(TimeProvider clock, OutboxRetryPolicy policy, CancellationToken cancellationToken)
    {
        _outbox = new InMemoryOutbox(clock, policy);
        return ValueTask.FromResult<IOutbox>(_outbox);
    }

    protected override ValueTask<IUnitOfWorkFactory> CreateUnitOfWorkFactory(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IUnitOfWorkFactory>(new InMemoryUnitOfWorkFactory());

    protected override IOutbox OutboxIn(IUnitOfWork work, TimeProvider clock, OutboxRetryPolicy policy) =>
        _outbox!.In((InMemoryUnitOfWork)work); // why: the contract calls CreateOutbox first
}
