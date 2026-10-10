using Aw.Kernel.Persistence;
using Xunit;

namespace Aw.Kernel.Testing;

/// <summary>
/// The contract every <see cref="IUnitOfWorkFactory"/> and its <see cref="IUnitOfWork"/> honour, run against the
/// in-memory fake (<see cref="InMemoryUnitOfWorkFactory"/>) and each real adapter alike, so a new store is proven by the
/// same tests (spec §4b.5, csharp.md Seams). A derived test class binds the members below to one implementation and
/// carries its lane: '[Lane(TestLane.Unit)]' for the fake, '[Lane(TestLane.Integration)]' for SQL. xUnit makes a new
/// instance per test, so each test gets a fresh store.
/// </summary>
public abstract class UnitOfWorkContract
{
    /// <summary>A factory over a fresh, empty store, created once per test instance.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The factory.</returns>
    protected abstract ValueTask<IUnitOfWorkFactory> CreateFactory(CancellationToken cancellationToken);

    /// <summary>Writes the record <paramref name="key"/> through <paramref name="work"/> (the implementation's write path).</summary>
    /// <param name="work">A unit of work the factory began.</param>
    /// <param name="key">The record to write; distinct keys are distinct records.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When the write is made.</returns>
    protected abstract ValueTask Write(IUnitOfWork work, int key, CancellationToken cancellationToken);

    /// <summary>Whether record <paramref name="key"/> is committed, read from outside any unit of work.</summary>
    /// <param name="key">The record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a committed unit wrote it.</returns>
    protected abstract ValueTask<bool> IsCommitted(int key, CancellationToken cancellationToken);

    /// <summary>A write is visible once its unit commits.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Commit_makes_the_writes_visible()
    {
        const string test = nameof(Commit_makes_the_writes_visible);
        var factory = await Checked(test, "CreateFactory", () => CreateFactory(Token));
        var work = await Begin(test, factory);
        await using (work.ConfigureAwait(false))
        {
            await Checked(test, "Write(1)", () => Write(work, 1, Token));
            await Commit(test, work);
        }

        if (!await Checked(test, "IsCommitted(1)", () => IsCommitted(1, Token)))
        {
            throw Violation(test, "record 1 was written and committed but is not visible.");
        }
    }

    /// <summary>A write is invisible outside its unit until the unit commits.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Writes_are_invisible_until_commit()
    {
        const string test = nameof(Writes_are_invisible_until_commit);
        var factory = await Checked(test, "CreateFactory", () => CreateFactory(Token));
        var work = await Begin(test, factory);
        await using (work.ConfigureAwait(false))
        {
            await Checked(test, "Write(1)", () => Write(work, 1, Token));
            if (await Checked(test, "IsCommitted(1)", () => IsCommitted(1, Token)))
            {
                throw Violation(test, "record 1 is visible outside its unit of work before Commit.");
            }

            await Commit(test, work);
        }
    }

    /// <summary>Disposing a unit without committing it rolls its writes back.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Disposing_without_a_commit_rolls_back()
    {
        const string test = nameof(Disposing_without_a_commit_rolls_back);
        var factory = await Checked(test, "CreateFactory", () => CreateFactory(Token));
        var work = await Begin(test, factory);
        await using (work.ConfigureAwait(false))
        {
            await Checked(test, "Write(1)", () => Write(work, 1, Token));
        }

        if (await Checked(test, "IsCommitted(1)", () => IsCommitted(1, Token)))
        {
            throw Violation(test, "record 1 is visible although its unit was disposed without Commit.");
        }
    }

    /// <summary>Disposing a committed unit keeps its writes.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Disposing_after_a_commit_keeps_the_writes()
    {
        const string test = nameof(Disposing_after_a_commit_keeps_the_writes);
        var factory = await Checked(test, "CreateFactory", () => CreateFactory(Token));
        var work = await Begin(test, factory);
        await Checked(test, "Write(1)", () => Write(work, 1, Token));
        await Commit(test, work);
        await Checked(test, "DisposeAsync", work.DisposeAsync);

        if (!await Checked(test, "IsCommitted(1)", () => IsCommitted(1, Token)))
        {
            throw Violation(test, "record 1 disappeared when its committed unit was disposed.");
        }
    }

    /// <summary>A second 'Commit' on the same unit is a bug and throws <see cref="InvalidOperationException"/>.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Committing_twice_throws()
    {
        const string test = nameof(Committing_twice_throws);
        var factory = await Checked(test, "CreateFactory", () => CreateFactory(Token));
        var work = await Begin(test, factory);
        await using (work.ConfigureAwait(false))
        {
            await Commit(test, work);
            try
            {
                _ = await work.Commit(Token);
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (Exception e)
            {
                throw Violation(test, $"a second Commit threw {e.GetType().Name}, not InvalidOperationException.", e);
            }

            throw Violation(test, "a second Commit on the same unit returned instead of throwing InvalidOperationException.");
        }
    }

    /// <summary>Units begun one after another commit or roll back independently.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Units_commit_or_roll_back_independently()
    {
        const string test = nameof(Units_commit_or_roll_back_independently);
        var factory = await Checked(test, "CreateFactory", () => CreateFactory(Token));
        var first = await Begin(test, factory);
        await using (first.ConfigureAwait(false))
        {
            await Checked(test, "Write(1)", () => Write(first, 1, Token));
            await Commit(test, first);
        }

        var second = await Begin(test, factory);
        await using (second.ConfigureAwait(false))
        {
            await Checked(test, "Write(2)", () => Write(second, 2, Token));
        }

        var third = await Begin(test, factory);
        await using (third.ConfigureAwait(false))
        {
            await Checked(test, "Write(3)", () => Write(third, 3, Token));
            await Commit(test, third);
        }

        var seen = (await Checked(test, "IsCommitted(1)", () => IsCommitted(1, Token)),
            await Checked(test, "IsCommitted(2)", () => IsCommitted(2, Token)),
            await Checked(test, "IsCommitted(3)", () => IsCommitted(3, Token)));
        if (seen != (true, false, true))
        {
            throw Violation(test, $"expected records 1 and 3 committed and 2 rolled back; saw 1={seen.Item1}, 2={seen.Item2}, 3={seen.Item3}.");
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContractViolationException Violation(string test, string detail, Exception? cause = null) =>
        cause is null
            ? new($"{nameof(UnitOfWorkContract)}.{test}: {detail}")
            : new($"{nameof(UnitOfWorkContract)}.{test}: {detail}", cause);

    private static async ValueTask<T> Checked<T>(string test, string call, Func<ValueTask<T>> run)
    {
        try
        {
            return await run();
        }
        catch (Exception e) when (e is not ContractViolationException)
        {
            throw Violation(test, $"{call} threw {e.GetType().Name}: {e.Message}", e);
        }
    }

    private static async ValueTask Checked(string test, string call, Func<ValueTask> run)
    {
        try
        {
            await run();
        }
        catch (Exception e) when (e is not ContractViolationException)
        {
            throw Violation(test, $"{call} threw {e.GetType().Name}: {e.Message}", e);
        }
    }

    private static async ValueTask<IUnitOfWork> Begin(string test, IUnitOfWorkFactory factory)
    {
        var begun = await Checked(test, "Begin", () => factory.Begin(Token));
        return begun.IsSuccess
            ? begun.Value
            : throw Violation(test, $"Begin on a fresh store returned {begun.Error.Kind} ({begun.Error.Code}): {begun.Error.Message}");
    }

    private static async ValueTask Commit(string test, IUnitOfWork work)
    {
        var committed = await Checked(test, "Commit", () => work.Commit(Token));
        if (committed.IsError)
        {
            throw Violation(test, $"Commit returned {committed.Error.Kind} ({committed.Error.Code}): {committed.Error.Message}");
        }
    }
}
