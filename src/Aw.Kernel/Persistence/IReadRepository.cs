namespace Aw.Kernel.Persistence;

/// <summary>
/// The small read base every aggregate repository shares (spec §4b.2): get one by id, or ask whether it exists.
/// Queries beyond these are named methods on the aggregate's own repository interface, never arbitrary filters.
/// </summary>
/// <typeparam name="TEntity">The aggregate or row type returned.</typeparam>
/// <typeparam name="TId">The strongly typed id.</typeparam>
public interface IReadRepository<TEntity, in TId>
    where TId : struct, IEquatable<TId>
{
    /// <summary>Loads the entity with <paramref name="id"/>.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entity, or a not-found error.</returns>
    public ValueTask<Result<TEntity>> Get(TId id, CancellationToken cancellationToken);

    /// <summary>Whether an entity with <paramref name="id"/> exists.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True or false, or an error when the store can't be read.</returns>
    public ValueTask<Result<bool>> Exists(TId id, CancellationToken cancellationToken);
}
