namespace Aw.Kernel.Persistence;

/// <summary>Begins a <see cref="IUnitOfWork"/>. The store's adapter implements it; the transaction decorator uses it.</summary>
public interface IUnitOfWorkFactory
{
    /// <summary>Begins a new unit of work.</summary>
    /// <param name="cancellationToken">Cancels the begin.</param>
    /// <returns>The unit of work, or the error the store reported.</returns>
    public ValueTask<Result<IUnitOfWork>> Begin(CancellationToken cancellationToken);
}
