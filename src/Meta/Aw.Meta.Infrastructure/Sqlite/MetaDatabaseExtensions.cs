using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>The two shapes every store call takes: one read, or one committed unit of work.</summary>
internal static class MetaDatabaseExtensions
{
    internal static async ValueTask<Result<IReadOnlyList<T>>> Query<T>(
        this MetaDatabase database,
        SqlStatement statement,
        Func<SqliteDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var begun = await database.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            return await SqliteQueries.List(begun.Value, statement, new RowMap<T>(map), cancellationToken).ConfigureAwait(false);
        }
    }

    // Runs the body in one transaction and commits when it succeeds.
    internal static async ValueTask<Result<T>> Write<T>(
        this MetaDatabase database,
        Func<SqliteUnitOfWork, ValueTask<Result<T>>> body,
        CancellationToken cancellationToken)
    {
        var begun = await database.BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var work = begun.Value;
        await using (work.ConfigureAwait(false))
        {
            var result = await body(work).ConfigureAwait(false);
            if (result.IsError)
            {
                return result;
            }

            var committed = await work.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? committed.Error : result;
        }
    }

    internal static ValueTask<Result<Unit>> Run(this MetaDatabase database, SqlStatement statement, CancellationToken cancellationToken) =>
        database.Write(
            async work =>
            {
                var changed = await SqliteQueries.Execute(work, statement, cancellationToken).ConfigureAwait(false);
                return changed.IsError ? changed.Error : Results.Ok();
            },
            cancellationToken);

    internal static SqliteCommand BindTime(this SqliteCommand command, string name, DateTimeOffset? value) =>
        value is { } time ? command.Bind(name, time) : command.Bind(name, (string?)null);

    internal static DateTimeOffset? ReadTime(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : SqliteRead.GetTimestamp(reader, ordinal);
}
