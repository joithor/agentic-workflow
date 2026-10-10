using Aw.Kernel.Persistence;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// The SQL implementation of <see cref="IReadRepository{TEntity,TId}"/>: a repository supplies its two queries
/// and how to bind its id, and gets Get and Exists. Its named queries sit next to them (spec §4b.2).
/// </summary>
/// <param name="session">The session to read through.</param>
/// <param name="mapper">Maps a row to the entity.</param>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The strongly typed id.</typeparam>
public abstract class SqliteReadRepository<TEntity, TId>(ISqliteSession session, IRowMapper<TEntity> mapper) : IReadRepository<TEntity, TId>
    where TId : struct, IEquatable<TId>
{
    /// <summary>The session this repository reads and writes through.</summary>
    protected ISqliteSession Session { get; } = session;

    /// <summary>The query for one entity by id. It selects the columns <see cref="IRowMapper{T}"/> reads.</summary>
    protected abstract string GetSql { get; }

    /// <summary>The query that returns 1 when the id exists, for example 'SELECT EXISTS (SELECT 1 FROM t WHERE id = $id)'.</summary>
    protected abstract string ExistsSql { get; }

    /// <inheritdoc />
    public ValueTask<Result<TEntity>> Get(TId id, CancellationToken cancellationToken) =>
        SqliteQueries.One(Session, Statement(GetSql, id), mapper, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<bool>> Exists(TId id, CancellationToken cancellationToken) =>
        SqliteQueries.Exists(Session, Statement(ExistsSql, id), cancellationToken);

    /// <summary>Binds <paramref name="id"/> to the queries' id parameter.</summary>
    /// <param name="command">The command.</param>
    /// <param name="id">The id.</param>
    protected abstract void BindId(SqliteCommand command, TId id);

    private SqlStatement Statement(string sql, TId id) =>
        new(sql, command => BindId(command, id), $"{typeof(TEntity).Name} {id}");
}
