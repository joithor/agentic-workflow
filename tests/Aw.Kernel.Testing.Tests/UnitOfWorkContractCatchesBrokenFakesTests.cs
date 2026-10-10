using Aw.Kernel.Persistence;

namespace Aw.Kernel.Testing.Tests;

/// <summary>The unit-of-work contract must fail a unit of work that drifts from the port's semantics.</summary>
[Lane(TestLane.Unit)]
public sealed class UnitOfWorkContractCatchesBrokenFakesTests
{
    [Fact]
    public async Task A_unit_that_writes_through_at_once_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new WritesThrough().Writes_are_invisible_until_commit);

        Assert.Contains("before Commit", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_unit_that_keeps_writes_without_a_commit_breaks_the_contract() =>
        await Assert.ThrowsAsync<ContractViolationException>(new WritesThrough().Disposing_without_a_commit_rolls_back);

    [Fact]
    public async Task A_unit_that_allows_a_second_commit_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new CommitsTwice().Committing_twice_throws);

        Assert.Contains("InvalidOperationException", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_unit_that_loses_its_writes_on_commit_breaks_the_contract() =>
        await Assert.ThrowsAsync<ContractViolationException>(new LosesWrites().Commit_makes_the_writes_visible);

    private abstract class Broken : UnitOfWorkContract
    {
        protected HashSet<int> Committed { get; } = [];

        protected override ValueTask<bool> IsCommitted(int key, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Committed.Contains(key));
    }

    private sealed class WritesThrough : Broken
    {
        protected override ValueTask<IUnitOfWorkFactory> CreateFactory(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IUnitOfWorkFactory>(new InMemoryUnitOfWorkFactory());

        protected override ValueTask Write(IUnitOfWork work, int key, CancellationToken cancellationToken)
        {
            _ = Committed.Add(key);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LosesWrites : Broken
    {
        protected override ValueTask<IUnitOfWorkFactory> CreateFactory(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IUnitOfWorkFactory>(new InMemoryUnitOfWorkFactory());

        protected override ValueTask Write(IUnitOfWork work, int key, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class CommitsTwice : Broken
    {
        protected override ValueTask<IUnitOfWorkFactory> CreateFactory(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IUnitOfWorkFactory>(new LenientFactory());

        protected override ValueTask Write(IUnitOfWork work, int key, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        private sealed class LenientFactory : IUnitOfWorkFactory
        {
            public ValueTask<Result<IUnitOfWork>> Begin(CancellationToken cancellationToken) =>
                ValueTask.FromResult<Result<IUnitOfWork>>(new LenientUnit());
        }

        private sealed class LenientUnit : IUnitOfWork
        {
            public ValueTask<Result<Unit>> Commit(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Ok());

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
