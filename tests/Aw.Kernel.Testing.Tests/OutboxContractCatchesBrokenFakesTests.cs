using Aw.Kernel.Outbox;

namespace Aw.Kernel.Testing.Tests;

/// <summary>
/// The outbox contract must fail an outbox that drifts. Each broken outbox wraps the correct fake and changes one
/// behavior, so a failure points at exactly that behavior.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class OutboxContractCatchesBrokenFakesTests
{
    [Fact]
    public async Task An_outbox_that_accepts_a_duplicate_id_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new Bound(o => new AcceptsDuplicates(o)).Recording_an_id_twice_is_a_conflict);

        Assert.Contains("AW-KERNEL-003", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_reads_newest_first_breaks_the_contract() =>
        await Assert.ThrowsAsync<ContractViolationException>(new Bound(o => new NewestFirst(o)).ReadPending_returns_the_oldest_first_up_to_max);

    [Fact]
    public async Task An_outbox_that_ignores_the_next_attempt_time_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new Bound(o => new IgnoresBackoff(o)).A_failed_command_waits_for_its_next_attempt);

        Assert.Contains("29 s after the first failure", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_never_dead_letters_breaks_the_contract() =>
        await Assert.ThrowsAsync<ContractViolationException>(new Bound(o => new IgnoresBackoff(o)).A_command_is_dead_lettered_when_the_policy_gives_up);

    [Fact]
    public async Task An_outbox_that_dispatches_twice_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new Bound(o => new DispatchesTwice(o)).A_dispatched_command_is_no_longer_pending);

        Assert.Contains("AW-KERNEL-002", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outbox_that_throws_for_max_zero_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new Bound(o => new ThrowsOnZero(o)).ReadPending_with_max_below_one_is_invalid);

        _ = Assert.IsType<ArgumentOutOfRangeException>(e.InnerException);
    }

    private sealed class Bound(Func<IOutbox, IOutbox> breaker) : OutboxContract
    {
        protected override ValueTask<IOutbox> CreateOutbox(TimeProvider clock, OutboxRetryPolicy policy, CancellationToken cancellationToken) =>
            ValueTask.FromResult(breaker(new InMemoryOutbox(clock, policy)));
    }

    private class Passthrough(IOutbox inner) : IOutbox
    {
        protected IOutbox Inner { get; } = inner;

        public virtual ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken) => Inner.Enqueue(command, cancellationToken);

        public virtual ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken) => Inner.ReadPending(max, cancellationToken);

        public virtual ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken) =>
            Inner.MarkDispatched(id, dispatchedAt, cancellationToken);

        public virtual ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken) => Inner.MarkFailed(id, error, cancellationToken);
    }

    private sealed class AcceptsDuplicates(IOutbox inner) : Passthrough(inner)
    {
        public override async ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken)
        {
            _ = await Inner.Enqueue(command, cancellationToken);
            return Results.Ok();
        }
    }

    private sealed class NewestFirst(IOutbox inner) : Passthrough(inner)
    {
        public override async ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken)
        {
            var all = await Inner.ReadPending(int.MaxValue, cancellationToken);
            return Results.Success<IReadOnlyList<OutboxCommand>>([.. all.Value.OrderByDescending(c => c.EnqueuedAt).Take(max)]);
        }
    }

    private sealed class IgnoresBackoff(IOutbox inner) : Passthrough(inner)
    {
        public override ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Ok());
    }

    private sealed class DispatchesTwice(IOutbox inner) : Passthrough(inner)
    {
        public override async ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken)
        {
            _ = await Inner.MarkDispatched(id, dispatchedAt, cancellationToken);
            return Results.Ok();
        }
    }

    private sealed class ThrowsOnZero(IOutbox inner) : Passthrough(inner)
    {
        public override ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
            return Inner.ReadPending(max, cancellationToken);
        }
    }
}
