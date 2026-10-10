using Aw.Kernel.Outbox;

namespace Aw.Kernel.Testing.Tests;

/// <summary>The outbox contract bound to the in-memory fake (unit lane). Aw.Kernel.Data.Tests binds it to SQLite.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryOutboxContractTests : OutboxContract
{
    protected override ValueTask<IOutbox> CreateOutbox(TimeProvider clock, OutboxRetryPolicy policy, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IOutbox>(new InMemoryOutbox(clock, policy));
}
