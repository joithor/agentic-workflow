using Aw.Kernel.Persistence;

namespace Aw.Kernel.Data;

/// <summary>Begins <see cref="SqliteUnitOfWork"/>s on one database.</summary>
/// <param name="connections">Opens the connection each unit of work owns.</param>
public sealed class SqliteUnitOfWorkFactory(ISqliteConnectionFactory connections) : IUnitOfWorkFactory
{
    /// <inheritdoc />
    public async ValueTask<Result<IUnitOfWork>> Begin(CancellationToken cancellationToken) =>
        (await BeginSession(cancellationToken).ConfigureAwait(false)).Map<IUnitOfWork>(work => work);

    /// <summary>Begins a unit of work and returns it as the session its repositories use.</summary>
    /// <param name="cancellationToken">Cancels the begin.</param>
    /// <returns>The unit of work, or AW-DATA-002 / AW-DATA-003.</returns>
    public ValueTask<Result<SqliteUnitOfWork>> BeginSession(CancellationToken cancellationToken) =>
        SqliteUnitOfWork.Begin(connections, cancellationToken);
}
