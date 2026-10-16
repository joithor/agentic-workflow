using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Intent;

/// <summary>
/// The intent layer in context.db (migration 0006_s5b_intent_links): what syncs wrote, outside the map versions. A page's records, links and cursor commit in one transaction, so a cursor never
/// moves past links that didn't commit and a replay writes nothing new.
/// </summary>
internal sealed class SqliteIntentStore(ContextDatabase database) : IIntentStore
{
    public async ValueTask<Result<IntentLayer>> Layer(CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : IntentLayer.Empty;
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var records = await SqliteQueries.List(read.Value, new SqlStatement(IntentSql.Records), IntentMappers.Record, cancellationToken).ConfigureAwait(false);
            var links = records.IsError ? records.Error : await SqliteQueries.List(read.Value, new SqlStatement(IntentSql.Links), IntentMappers.Link, cancellationToken).ConfigureAwait(false);
            var decisions = links.IsError ? links.Error : await SqliteQueries.List(read.Value, new SqlStatement(IntentSql.Decisions), IntentMappers.Decision, cancellationToken).ConfigureAwait(false);
            var edges = decisions.IsError ? decisions.Error : await SqliteQueries.List(read.Value, new SqlStatement(IntentSql.Edges), IntentMappers.Edge, cancellationToken).ConfigureAwait(false);
            return edges.IsError ? ContextDatabase.Translate(edges.Error) : new IntentLayer(records.Value, links.Value, decisions.Value, edges.Value);
        }
    }

    public async ValueTask<Result<IntentSourceState>> State(SourceId source, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : new IntentSourceState(Cursor: null, LastSync: null, LastError: null, Records: 0, Links: 0, Decisions: 0, Queued: 0);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var state = await SqliteQueries.One(read.Value, new SqlStatement(IntentSql.State, c => c.Bind("@source", source.Value)), IntentMappers.State, cancellationToken).ConfigureAwait(false);
            return state.IsError ? ContextDatabase.Translate(state.Error) : state.Value;
        }
    }

    public async ValueTask<Result<IntentCommit>> CommitPage(IntentPage page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var changed = 0;
            foreach (var statement in Statements(page))
            {
                var done = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return ContextDatabase.Translate(done.Error);
                }

                changed += statement.Sql == IntentSql.UpsertRecord ? done.Value : 0;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : new IntentCommit(page.Records.Count, page.Links.Count, changed);
        }
    }

    public async ValueTask<Result<Unit>> RecordFailure(SourceId source, string code, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var done = await SqliteQueries.Execute(begun.Value, new SqlStatement(IntentSql.Failed, c => c.Bind("@source", source.Value).Bind("@code", code)), cancellationToken).ConfigureAwait(false);
            var committed = done.IsError ? done.Error : await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    private static IEnumerable<SqlStatement> Statements(IntentPage page)
    {
        foreach (var record in page.Records)
        {
            yield return new SqlStatement(IntentSql.UpsertRecord, c => c.Bind("@key", record.Key.Value).Bind("@source", record.Source.Value).Bind("@version", record.Version)
                .Bind("@title", record.Title).Bind("@aliases", IntentMappers.Joined(record.Aliases)).Bind("@at", page.At));
            yield return new SqlStatement(IntentSql.DeleteLinks, c => c.Bind("@record", record.Key.Value));
        }

        foreach (var link in page.Links)
        {
            yield return new SqlStatement(IntentSql.InsertLink, c => c.Bind("@record", link.Record.Value).Bind("@target", link.Target.Value).Bind("@rule", IntentMappers.Of(link.Rule))
                .Bind("@confidence", link.Confidence).Bind("@span", link.SpanHash).Bind("@at", page.At));
        }

        yield return new SqlStatement(IntentSql.Synced, c => c.Bind("@source", page.Source.Value).Bind("@cursor", page.Cursor).Bind("@at", page.At));
    }
}
