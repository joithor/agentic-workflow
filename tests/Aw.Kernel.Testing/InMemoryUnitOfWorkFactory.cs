using Aw.Kernel.Persistence;

namespace Aw.Kernel.Testing;

/// <summary>
/// The in-memory fake of <see cref="IUnitOfWorkFactory"/>: begins <see cref="InMemoryUnitOfWork"/>s. A package's
/// fake repositories enlist their writes in the unit they're given, so a use case's writes land only on commit, as
/// with SQL. Checked by <see cref="UnitOfWorkContract"/>.
/// </summary>
public sealed class InMemoryUnitOfWorkFactory : IUnitOfWorkFactory
{
    /// <inheritdoc />
    public ValueTask<Result<IUnitOfWork>> Begin(CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: InMemoryUnitOfWorkFactory.Begin");
}
