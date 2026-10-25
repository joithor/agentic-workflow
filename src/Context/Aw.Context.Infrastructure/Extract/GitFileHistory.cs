using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>
/// The landed changes of a set of paths over <see cref="GitCli"/> (read-only git, the caller's GIT_* variables removed, never the working tree). The window is applied here
/// on the committer date, both ends inclusive, because git's own date limits stop at the first out-of-order commit. A repo git can't read is SND-CONTEXT-048.
/// </summary>
internal sealed class GitFileHistory(GitCli git) : IFileHistory
{
    public async ValueTask<Result<FileHistory>> Changes(ContextRepo repo, FileHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(query);
        if (Invalid(query) is { } invalid)
        {
            return invalid;
        }

        var head = await git.Head(repo.Path, cancellationToken).ConfigureAwait(false);
        var shallow = head.IsError ? head.Error : await git.IsShallow(repo.Path, cancellationToken).ConfigureAwait(false);
        if (shallow.IsError)
        {
            return Unreadable(repo);
        }

        if (query.Paths.Count == 0)
        {
            return new FileHistory(head.Value, shallow.Value, [], Truncated: false);
        }

        var start = query.UpTo is null ? head : await git.Commit(repo.Path, query.UpTo, cancellationToken).ConfigureAwait(false);
        if (start.IsError)
        {
            return start.Error.Code == ContextErrors.CommitUnknown.Code ? start.Error : Unreadable(repo);
        }

        var log = await git.FileLog(repo.Path, start.Value, query.Paths, cancellationToken).ConfigureAwait(false);
        return log.IsError ? Unreadable(repo) : Select(head.Value, shallow.Value, Parse(log.Value, query.Paths.ToHashSet(StringComparer.Ordinal)), query);
    }

    private static Error? Invalid(FileHistoryQuery query) => query switch
    {
        _ when query.Paths.Any(p => p.Length == 0 || p.Any(char.IsControl)) => ContextErrors.QueryInvalid.With("a path is empty or holds a control character"),
        { UpTo: { } bound } when !GitCli.IsSha(bound) => ContextErrors.QueryInvalid.With("the upper bound isn't a commit sha"),
        _ => null,
    };

    private static FileHistory Select(string head, bool shallow, List<FileChange> found, FileHistoryQuery query)
    {
        var inside = found.Where(c => query.Window.NotAfterCut(c.At) && (query.Window.Since is not { } since || c.At >= since)).Take(query.Max + 1).ToList();
        return new FileHistory(head, shallow, [.. inside.Take(query.Max)], inside.Count > query.Max);
    }

    // A record that doesn't read as a commit (a subject that carries the record-start character splits it) is skipped, never guessed at.
    private static List<FileChange> Parse(string text, HashSet<string> wanted)
    {
        var changes = new List<FileChange>();
        foreach (var record in text.Split(GitCli.LogRecordStart, StringSplitOptions.RemoveEmptyEntries))
        {
            var end = record.IndexOf('\0', StringComparison.Ordinal);
            var header = (end < 0 ? record : record[..end]).TrimEnd('\n').Split(GitCli.LogFieldSeparator, 4);
            if (header.Length != 4 || header[0].Length != 40 || !GitCli.IsSha(header[0])
                || !DateTimeOffset.TryParse(header[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                continue;
            }

            var files = end < 0 ? [] : record[(end + 1)..].Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(f => f.TrimStart('\n')).Where(wanted.Contains).Order(StringComparer.Ordinal).ToList();
            if (files.Count > 0)
            {
                changes.Add(new FileChange(header[0], at, header[3], header[2].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, files));
            }
        }

        return changes;
    }

    private static Error Unreadable(ContextRepo repo) =>
        ContextErrors.ExtractionUnreadable.With($"git can't read the history of {repo.Name.Value} (fix: check the checkout)");   // names the repo, never what git printed
}
