using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>The repo-docs writes, each in one short transaction.</summary>
internal static class DocWriter
{
    /// <summary>Removes the batch's stale documents, writes its prepared ones and moves the repo's cursor to its head.</summary>
    internal static async ValueTask<Result<Unit>> Write(SqliteUnitOfWorkFactory sessions, DocBatch batch, CancellationToken cancellationToken)
    {
        var (repo, head, removed, prepared, at) = batch;
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var session = begun.Value;
            var statements = removed.SelectMany(path => (SqlStatement[])[
                    new(DocSql.DeleteChunks, c => c.Bind("@repo", repo).Bind("@path", path)),
                    new(DocSql.DeleteDoc, c => c.Bind("@repo", repo).Bind("@path", path))])
                .Concat(prepared.SelectMany(doc => Inserts(repo, doc)))
                .Append(new SqlStatement(DocSql.Synced, c => c.Bind("@source", DocSql.Source).Bind("@repo", repo).Bind("@cursor", head).Bind("@at", at)));
            foreach (var statement in statements)
            {
                var done = await SqliteQueries.Execute(session, statement, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return done.Error;
                }
            }

            return await session.Commit(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Records that the repo couldn't be refreshed, keeping its last good cursor and sync time.</summary>
    internal static async ValueTask<Result<Unit>> RecordFailure(SqliteUnitOfWorkFactory sessions, string repo, string code, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var done = await SqliteQueries.Execute(begun.Value, new SqlStatement(DocSql.Failed,
                c => c.Bind("@source", DocSql.Source).Bind("@repo", repo).Bind("@code", code)), cancellationToken).ConfigureAwait(false);
            return done.IsError ? done.Error : await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<SqlStatement> Inserts(string repo, PreparedDoc doc) =>
        doc.Chunks.Select(chunk => new SqlStatement(DocSql.InsertChunk, c => c.Bind("@repo", repo).Bind("@path", doc.Path).Bind("@blob", doc.Blob)
                .Bind("@start", chunk.LineStart).Bind("@end", chunk.LineEnd).Bind("@text", chunk.Text)))
            .Prepend(new SqlStatement(DocSql.InsertDoc, c => c.Bind("@repo", repo).Bind("@path", doc.Path).Bind("@blob", doc.Blob)
                .Bind("@title", doc.Title).Bind("@blocked", doc.Blocked)));
}
