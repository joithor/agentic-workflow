using Aw.Kernel.Persistence;

namespace Aw.Kernel.Testing;

/// <summary>
/// The in-memory fake of <see cref="IUnitOfWorkFactory"/>: begins <see cref="InMemoryUnitOfWork"/>s. A package's
/// fake repositories enlist their writes in the unit they're given, so a use case's writes land only on commit, as
/// with SQL. Checked by <see cref="UnitOfWorkContract"/>.
/// </summary>
public sealed class InMemoryUnitOfWorkFactory : IUnitOfWorkFactory
{
    /// <summary>How many units have committed, for tests that assert a use case committed (or didn't).</summary>
    public int Commits { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<IUnitOfWork>> Begin(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Result<IUnitOfWork>>(new InMemoryUnitOfWork(() => Commits++));
    }
}
