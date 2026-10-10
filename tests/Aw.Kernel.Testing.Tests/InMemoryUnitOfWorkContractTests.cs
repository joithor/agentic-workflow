using Aw.Kernel.Persistence;

namespace Aw.Kernel.Testing.Tests;

/// <summary>The unit-of-work contract bound to the in-memory fake (unit lane). Aw.Kernel.Data.Tests binds it to SQLite.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryUnitOfWorkContractTests : UnitOfWorkContract
{
    private readonly HashSet<int> _committed = [];

    protected override ValueTask<IUnitOfWorkFactory> CreateFactory(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IUnitOfWorkFactory>(new InMemoryUnitOfWorkFactory());

    protected override ValueTask Write(IUnitOfWork work, int key, CancellationToken cancellationToken)
    {
        ((InMemoryUnitOfWork)work).Enlist(() => _committed.Add(key));
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<bool> IsCommitted(int key, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_committed.Contains(key));
}
