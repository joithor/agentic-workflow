using Aw.Kernel.Persistence;

namespace Aw.Kernel.Testing;

/// <summary>One in-memory unit of work: enlisted writes run, in order, on <see cref="Commit"/>; disposing without a commit drops them.</summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    /// <summary>Enlists a write to apply when the unit commits.</summary>
    /// <param name="apply">The write.</param>
    public void Enlist(Action apply) => throw new NotSupportedException("red: InMemoryUnitOfWork.Enlist");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Commit(CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: InMemoryUnitOfWork.Commit");

    /// <inheritdoc />
    public ValueTask DisposeAsync() => throw new NotSupportedException("red: InMemoryUnitOfWork.DisposeAsync");
}
