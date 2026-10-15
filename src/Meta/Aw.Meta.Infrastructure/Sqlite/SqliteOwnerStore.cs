using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Meta.Core;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Owner;
using Aw.Meta.Core.Ports;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IOwnerStore over meta.db (the owner_questions table).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteOwnerStore(MetaDatabase database) : IOwnerStore
{
    private const string Columns = "SELECT number, asked_by, text, asked_at, answer, answered_at FROM owner_questions ";

    public ValueTask<Result<int>> Ask(string askedBy, string text, DateTimeOffset at, CancellationToken cancellationToken) =>
        database.Write(
            async work =>
            {
                var insert = new SqlStatement(
                    "INSERT INTO owner_questions (asked_by, text, asked_at) VALUES ($by, $text, $at)",
                    c => c.Bind("$by", askedBy).Bind("$text", text).Bind("$at", at),
                    "owner question");
                var changed = await SqliteQueries.Execute(work, insert, cancellationToken).ConfigureAwait(false);
                if (changed.IsError)
                {
                    return changed.Error;
                }

                var number = new SqlStatement("SELECT last_insert_rowid()", Subject: "owner question number");
                var row = await SqliteQueries.One(work, number, new RowMap<int>(r => checked((int)r.GetInt64(0))), cancellationToken).ConfigureAwait(false);
                return row;
            },
            cancellationToken);

    public ValueTask<Result<IReadOnlyList<OwnerQuestion>>> List(bool includeAnswered, CancellationToken cancellationToken) =>
        database.Query(
            new SqlStatement(Columns + (includeAnswered ? string.Empty : "WHERE answer IS NULL ") + "ORDER BY number ASC", Subject: "owner questions"),
            Read,
            cancellationToken);

    public ValueTask<Result<OwnerQuestion>> Answer(int number, string text, DateTimeOffset at, CancellationToken cancellationToken) =>
        database.Write(
            async work =>
            {
                var update = new SqlStatement(
                    "UPDATE owner_questions SET answer = $answer, answered_at = $at WHERE number = $number",
                    c => c.Bind("$answer", text).Bind("$at", at).Bind("$number", number),
                    "owner question");
                var changed = await SqliteQueries.Execute(work, update, cancellationToken).ConfigureAwait(false);
                if (changed.IsError)
                {
                    return changed.Error;
                }

                if (changed.Value == 0)
                {
                    return MetaErrors.QuestionNotFound.With("no owner question " + number.ToString(CultureInfo.InvariantCulture));
                }

                var select = new SqlStatement(Columns + "WHERE number = $number", c => c.Bind("$number", number), "owner question");
                return await SqliteQueries.One(work, select, new RowMap<OwnerQuestion>(Read), cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public async ValueTask<Result<IReadOnlyList<OwnerAnswer>>> Answers(CancellationToken cancellationToken)
    {
        var rows = await database.Query(
            new SqlStatement(Columns + "WHERE answer IS NOT NULL ORDER BY number ASC", Subject: "owner answers"),
            Read,
            cancellationToken).ConfigureAwait(false);
        return rows.IsError
            ? rows.Error
            : Results.Success<IReadOnlyList<OwnerAnswer>>([.. rows.Value.Select(q => new OwnerAnswer(q.Number, q.AskedBy, q.Answer!, q.AnsweredAt!.Value))]); // why: the query keeps only answered rows
    }

    private static OwnerQuestion Read(SqliteDataReader r) =>
        new(checked((int)r.GetInt64(0)), r.GetString(1), r.GetString(2), SqliteRead.GetTimestamp(r, 3))
        {
            Answer = r.IsDBNull(4) ? null : r.GetString(4),
            AnsweredAt = r.ReadTime(5),
        };
}
