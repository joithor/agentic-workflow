using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// The repo-docs index in context.db (spec §5.2, §6.2): at each refresh, the documents the globs match at the repo's
/// HEAD are read from git and cleaned at the ingest boundary first (sanitized and scrubbed as a whole, then chunked), then written
/// in one short transaction, so text reaches the store only scrubbed and no sync waits on a long writer. A document
/// whose blob didn't change is kept; one gone from HEAD or the globs is removed; one the scrubber blocks keeps its row
/// without chunks. A repo git can't read records its code and is reported, not thrown.
/// </summary>
internal sealed class SqliteDocIndex(ContextDatabase database, GitCli git, IIngestFilter ingest, TimeProvider clock) : IDocIndex
{
    /// <summary>The largest document indexed (bigger ones are skipped).</summary>
    internal const long MaxDocumentBytes = 1024 * 1024;

    public async ValueTask<Result<RepoRefresh>> Refresh(ContextRepo repo, IReadOnlyList<string> globs, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var head = await git.Head(repo.Path, cancellationToken).ConfigureAwait(false);
        var tree = head.IsError ? head.Error : await git.Tree(repo.Path, head.Value, cancellationToken).ConfigureAwait(false);
        if (tree.IsError)
        {
            return await Failed(writable.Value, repo, tree.Error.Code, cancellationToken).ConfigureAwait(false);
        }

        var stored = await Stored(writable.Value, repo, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return ContextDatabase.Translate(stored.Error);
        }

        var glob = new Glob(globs);
        var wanted = tree.Value.Where(e => glob.Matches(e.Key)).ToDictionary(StringComparer.Ordinal);
        var changed = wanted.Where(w => !stored.Value.TryGetValue(w.Key, out var blob) || !string.Equals(blob, w.Value, StringComparison.Ordinal)).ToList();
        var (prepared, unreadable) = await Prepare(repo, changed, cancellationToken).ConfigureAwait(false);
        var removed = stored.Value.Keys.Where(p => !wanted.ContainsKey(p) || (!string.Equals(stored.Value[p], wanted[p], StringComparison.Ordinal) && !unreadable.Contains(p))).ToList();
        var written = await DocWriter.Write(writable.Value, new DocBatch(repo.Name.Value, head.Value, removed, prepared, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        database.RestrictFiles();
        return written.IsError
            ? ContextDatabase.Translate(written.Error)
            : new RepoRefresh(repo.Name, SourceVersion.From(head.Value), prepared.Count(p => p is { Blocked: false, Oversized: false }),
                removed.Count(p => !wanted.ContainsKey(p)), wanted.Count - changed.Count, prepared.Count(p => p.Blocked), Code: null)
            {
                Skipped = prepared.Count(p => p.Oversized) + unreadable.Count,
            };
    }

    public async ValueTask<Result<DocIndexStatus>> Status(ContextRepo repo, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : new DocIndexStatus(Head: null, LastSync: null, Code: null, 0, 0);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var status = await SqliteQueries.One(read.Value, new SqlStatement(DocSql.Status,
                c => c.Bind("@source", DocSql.Source).Bind("@repo", repo.Name.Value)), DocMappers.Status, cancellationToken).ConfigureAwait(false);
            return status.IsError ? ContextDatabase.Translate(status.Error) : status.Value;
        }
    }

    /// <summary>
    /// Reads and cleans each changed document. One over <see cref="MaxDocumentBytes"/> keeps a row without chunks (so it
    /// counts as kept until it changes) and is reported skipped; one git can't read this run is skipped and its earlier
    /// rows stay.
    /// </summary>
    private async ValueTask<(List<PreparedDoc> Prepared, HashSet<string> Unreadable)> Prepare(ContextRepo repo, List<KeyValuePair<string, string>> changed,
        CancellationToken cancellationToken)
    {
        var prepared = new List<PreparedDoc>();
        var unreadable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, blob) in changed)
        {
            var size = await git.Size(repo.Path, blob, cancellationToken).ConfigureAwait(false);
            var text = size is { IsSuccess: true, Value: <= MaxDocumentBytes } ? await git.Blob(repo.Path, blob, cancellationToken).ConfigureAwait(false) : null;
            if (size.IsSuccess && size.Value > MaxDocumentBytes)
            {
                prepared.Add(new PreparedDoc(path, blob, path, Blocked: false, []) { Oversized = true });
            }
            else if (text is { IsSuccess: true })
            {
                prepared.Add(Clean(path, blob, text.Value));
            }
            else
            {
                _ = unreadable.Add(path);
            }
        }

        return (prepared, unreadable);
    }

    /// <summary>Cleans the whole document once (line-preserving), then chunks the clean text and takes the title from it.</summary>
    private PreparedDoc Clean(string path, string blob, string text)
    {
        var clean = ingest.Clean(text, singleLine: false);
        if (clean.Blocked)
        {
            return new PreparedDoc(path, blob, path, Blocked: true, []);
        }

        var title = ingest.Clean(DocChunker.Title(clean.Text, path), singleLine: true);
        return new PreparedDoc(path, blob, title.Blocked ? path : UntrustedFence.Cap(title.Text), Blocked: false, DocChunker.Split(clean.Text));
    }

    private static async ValueTask<Result<Dictionary<string, string>>> Stored(SqliteUnitOfWorkFactory sessions, ContextRepo repo, CancellationToken cancellationToken)
    {
        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, new SqlStatement(DocSql.StoredDocs, c => c.Bind("@repo", repo.Name.Value)), DocMappers.Stored, cancellationToken)
                .ConfigureAwait(false);
            return rows.Map(r => r.ToDictionary(d => d.Path, d => d.Blob, StringComparer.Ordinal));
        }
    }

    private static async ValueTask<Result<RepoRefresh>> Failed(SqliteUnitOfWorkFactory sessions, ContextRepo repo, string code, CancellationToken cancellationToken)
    {
        var recorded = await DocWriter.RecordFailure(sessions, repo.Name.Value, code, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? ContextDatabase.Translate(recorded.Error) : new RepoRefresh(repo.Name, Head: null, 0, 0, 0, 0, code);
    }
}
