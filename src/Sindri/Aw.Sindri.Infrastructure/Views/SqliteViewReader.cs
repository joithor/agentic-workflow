using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The queries' read side of views.db (read sessions only: busy_timeout and query_only). It never creates the file: a
/// missing views.db has no state, no rows and no error (doctor and queries before the first start).
/// </summary>
internal sealed class SqliteViewReader(ViewsDatabase database, JsonSerializerOptions options) : IViewReader
{
    public async ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class =>
        database.Exists
            ? await Read(rows => rows.Get<TRow>(view, key, cancellationToken), cancellationToken).ConfigureAwait(false)
            : Results.Success<TRow?>(value: null);

    public async ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class =>
        database.Exists
            ? await Read(rows => rows.List<TRow>(view, cancellationToken), cancellationToken).ConfigureAwait(false)
            : Results.Success<IReadOnlyList<TRow>>([]);

    public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class =>
        throw new NotSupportedException("SqliteViewReader.Range: P3 unit B makes Range_returns_keys_between_the_bounds_in_order pass");

    public async ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken)
    {
        if (!database.Exists)
        {
            return Results.Success<ProjectorState?>(value: null);
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ViewsDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var state = await SqliteQueries.List(read.Value, new SqlStatement(ViewSql.SelectState), ProjectorStateMapper.Instance, cancellationToken)
                .ConfigureAwait(false);
            return state.IsError ? ViewsDatabase.Translate(state.Error) : Results.Success(state.Value.Count == 0 ? null : state.Value[0]);
        }
    }

    private async ValueTask<Result<T>> Read<T>(Func<SqliteViewRows, ValueTask<Result<T>>> query, CancellationToken cancellationToken)
    {
        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ViewsDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            return await query(new SqliteViewRows(read.Value, 0, options)).ConfigureAwait(false);
        }
    }
}
