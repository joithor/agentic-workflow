using System.Globalization;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

internal sealed partial class GitCli
{
    private const char FieldSeparator = '\u001f';
    private const char RecordSeparator = '\u001e';

    /// <summary>The first-parent commits reachable from HEAD, oldest first.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The commits, or SND-CONTEXT-010 when git can't read the repo.</returns>
    public async ValueTask<Result<IReadOnlyList<GitLogEntry>>> FirstParentLog(string repo, CancellationToken cancellationToken)
    {
        var format = $"--format=%H{FieldSeparator}%cI{FieldSeparator}%P{FieldSeparator}%s{RecordSeparator}";
        var log = await Text(repo, ["log", "--first-parent", "--reverse", format, "HEAD"], cancellationToken).ConfigureAwait(false);
        return log.Bind<IReadOnlyList<GitLogEntry>>(text => Parse(text) is { } entries ? entries : ContextErrors.SourceUnavailable.With("git log didn't answer the expected shape"));
    }

    /// <summary>The files a merge changed against its first parent.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="merge">The merge commit's sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The '/'-separated paths in ordinal order, or SND-CONTEXT-010.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> ChangedAgainstFirstParent(string repo, string merge, CancellationToken cancellationToken)
    {
        if (!IsSha(merge))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var diff = await Text(repo, ["diff", "--name-only", "-z", $"{merge}^1", merge], cancellationToken).ConfigureAwait(false);
        return diff.Map<IReadOnlyList<string>>(text => [.. text.Split('\0', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)]);
    }

    private static List<GitLogEntry>? Parse(string text)
    {
        var entries = new List<GitLogEntry>();
        foreach (var record in text.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim('\n')).Where(r => r.Length > 0))
        {
            var fields = record.Split(FieldSeparator);
            if (fields.Length != 4 || !IsSha(fields[0]) || !DateTimeOffset.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                return null;
            }

            entries.Add(new GitLogEntry(fields[0], at, fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, fields[3]));
        }

        return entries;
    }
}
