using Aw.Kernel.Persistence;

namespace Aw.Kernel.Testing;

/// <summary>
/// One in-memory unit of work: enlisted writes run, in order, on <see cref="Commit"/>; disposing without a commit drops
/// them. Not thread-safe, like a unit of work (one per use case).
/// </summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly List<Action> _writes = [];
    private readonly Action _onCommitted;
    private bool _committed;
    private bool _disposed;

    internal InMemoryUnitOfWork(Action onCommitted)
    {
        _onCommitted = onCommitted;
    }

    /// <summary>Enlists a write to apply when the unit commits.</summary>
    /// <param name="apply">The write.</param>
    /// <exception cref="InvalidOperationException">The unit is already committed or disposed.</exception>
    public void Enlist(Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ThrowIfClosed();
        _writes.Add(apply);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The unit is already committed or disposed.</exception>
    public ValueTask<Result<Unit>> Commit(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfClosed();
        foreach (var write in _writes)
        {
            write();
        }

        _writes.Clear();
        _committed = true;
        _onCommitted();
        return ValueTask.FromResult(Results.Ok());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _writes.Clear();
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private void ThrowIfClosed()
    {
        if (_committed)
        {
            throw new InvalidOperationException("This unit of work is already committed; begin a new one.");
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
