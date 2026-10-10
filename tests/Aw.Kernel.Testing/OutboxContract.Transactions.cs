using Aw.Kernel.Outbox;
using Aw.Kernel.Persistence;
using Xunit;

namespace Aw.Kernel.Testing;

/// <summary>The transactional and ordering half of <see cref="OutboxContract"/>.</summary>
public abstract partial class OutboxContract
{
    /// <summary>A unit-of-work factory over the same store <see cref="CreateOutbox"/> created (called after it).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The factory.</returns>
    protected abstract ValueTask<IUnitOfWorkFactory> CreateUnitOfWorkFactory(CancellationToken cancellationToken);

    /// <summary>An outbox over the same store whose writes join <paramref name="work"/> (the use case's unit of work).</summary>
    /// <param name="work">A unit the factory began.</param>
    /// <param name="clock">The contract's clock.</param>
    /// <param name="policy">The retry policy.</param>
    /// <returns>The outbox.</returns>
    protected abstract IOutbox OutboxIn(IUnitOfWork work, TimeProvider clock, OutboxRetryPolicy policy);

    /// <summary>A command enqueued in a unit of work is pending only once the unit commits.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_command_enqueued_in_a_unit_of_work_lands_with_its_commit()
    {
        const string test = nameof(A_command_enqueued_in_a_unit_of_work_lands_with_its_commit);
        var outbox = await Create(test);
        var factory = await Checked(test, "CreateUnitOfWorkFactory", () => CreateUnitOfWorkFactory(Token));
        var work = await Begin(test, factory);
        await using (work.ConfigureAwait(false))
        {
            await Enqueue(test, OutboxIn(work, _clock, Policy), Command(1, KnownInstants.Epoch));
            if ((await ReadPending(test, outbox, 10)).Count != 0)
            {
                throw Violation(test, "a command enqueued in a unit of work is pending before the unit commits.");
            }

            var committed = await Checked(test, "Commit", () => work.Commit(Token));
            if (committed.IsError)
            {
                throw Violation(test, $"Commit returned {committed.Error.Code}: {committed.Error.Message}");
            }
        }

        if ((await ReadPending(test, outbox, 10)).Count != 1)
        {
            throw Violation(test, "a command enqueued in a committed unit of work is not pending.");
        }
    }

    /// <summary>A command enqueued in a unit of work that is disposed without a commit is gone.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_command_enqueued_in_a_rolled_back_unit_of_work_is_gone()
    {
        const string test = nameof(A_command_enqueued_in_a_rolled_back_unit_of_work_is_gone);
        var outbox = await Create(test);
        var factory = await Checked(test, "CreateUnitOfWorkFactory", () => CreateUnitOfWorkFactory(Token));
        var work = await Begin(test, factory);
        await using (work.ConfigureAwait(false))
        {
            await Enqueue(test, OutboxIn(work, _clock, Policy), Command(1, KnownInstants.Epoch));
        }

        if ((await ReadPending(test, outbox, 10)).Count != 0)
        {
            throw Violation(test, "a command enqueued in a unit of work disposed without Commit is still pending.");
        }
    }

    /// <summary>Times come back in UTC (offset zero) at the same instant, whatever offset they were recorded with.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Times_are_read_back_in_utc()
    {
        const string test = nameof(Times_are_read_back_in_utc);
        var outbox = await Create(test);
        var local = new DateTimeOffset(2025, 12, 31, 23, 0, 0, TimeSpan.FromHours(-2));
        await Enqueue(test, outbox, Command(1, local));

        var read = (await ReadPending(test, outbox, 10)).Single();
        if (read.EnqueuedAt.Offset != TimeSpan.Zero || read.EnqueuedAt != local)
        {
            throw Violation(test, $"EnqueuedAt {local:O} came back as {read.EnqueuedAt:O}; it must be the same instant at offset zero.");
        }
    }

    /// <summary>Commands with the same enqueue time are read in the order they were enqueued.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Commands_enqueued_at_the_same_time_are_read_in_enqueue_order()
    {
        const string test = nameof(Commands_enqueued_at_the_same_time_are_read_in_enqueue_order);
        var outbox = await Create(test);
        var first = Command(2, KnownInstants.Epoch);
        var second = Command(1, KnownInstants.Epoch);
        await Enqueue(test, outbox, first);
        await Enqueue(test, outbox, second);

        var pending = await ReadPending(test, outbox, 10);
        if (pending.Count != 2 || pending[0].Id != first.Id || pending[1].Id != second.Id)
        {
            throw Violation(test, $"two commands at the same EnqueuedAt must come back in enqueue order ({first.Id}, {second.Id}); got {Describe(pending)}.");
        }
    }

    private static async ValueTask<IUnitOfWork> Begin(string test, IUnitOfWorkFactory factory)
    {
        var begun = await Checked(test, "Begin", () => factory.Begin(Token));
        return begun.IsSuccess ? begun.Value : throw Violation(test, $"Begin returned {begun.Error.Code}: {begun.Error.Message}");
    }
}
