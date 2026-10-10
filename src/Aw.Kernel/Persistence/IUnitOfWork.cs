namespace Aw.Kernel.Persistence;

/// <summary>
/// One transaction around the writes of one use case (spec §4b.2). The transaction decorator begins it,
/// the service's repositories write through it, and it commits once. Disposing it without a commit rolls back.
/// Every implementation passes Aw.Kernel.Testing's UnitOfWorkContract.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>Commits every write made through this unit of work.</summary>
    /// <param name="cancellationToken">Cancels the commit.</param>
    /// <returns>Success, or the error the store reported (the writes are then rolled back).</returns>
    /// <exception cref="InvalidOperationException">The unit is already committed (a second commit is a bug).</exception>
    public ValueTask<Result<Unit>> Commit(CancellationToken cancellationToken);
}
