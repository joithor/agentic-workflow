using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The in-memory <see cref="IChangeSource"/>: one branch, <c language="text">feat/expiry</c>, over a base, as a git checkout would answer it (changed paths sorted, denied paths listed with no hunk, the diff cut at the cap).</summary>
public sealed class InMemoryChangeSource : IChangeSource
{
    private const string Branch = "feat/expiry";

    private readonly IReadOnlyList<ChangeSeedCommit> _commits;

    /// <summary>A source whose <c language="text">feat/expiry</c> branch adds the commits to the base files.</summary>
    /// <param name="baseFiles">The files on the base.</param>
    /// <param name="commits">The branch's commits, oldest first.</param>
    public InMemoryChangeSource(IReadOnlyDictionary<string, string> baseFiles, IReadOnlyList<ChangeSeedCommit> commits)
    {
        ArgumentNullException.ThrowIfNull(baseFiles);
        ArgumentNullException.ThrowIfNull(commits);
        _commits = commits;
    }

    /// <inheritdoc/>
    public ValueTask<Result<ChangeRecord>> Read(string checkout, string reference, ChangeRead read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(read);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(reference, Branch, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<ChangeRecord>>(SindriErrors.InvalidInput.With($"the checkout has no branch or PR '{reference}'"));
        }

        var final = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, text) in _commits.SelectMany(static c => c.Files))
        {
            final[path] = text;
        }

        var diff = new StringBuilder();
        foreach (var (path, text) in final.Where(static f => !BriefPaths.Denied(f.Key)))
        {
            _ = diff.Append(CultureInfo.InvariantCulture, $"diff --git a/{path} b/{path}\n+++ b/{path}\n").Append(string.Concat(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => "+" + l + "\n")));
        }

        var (cut, truncated) = read.Cut(diff.ToString());
        var head = "head" + _commits.Count.ToString(CultureInfo.InvariantCulture);
        return ValueTask.FromResult<Result<ChangeRecord>>(new ChangeRecord(
            reference, "base", head, PullRequestBody: null, [.. _commits.Select(static c => c.Message)], [.. final.Keys.Where(static p => !BriefPaths.Denied(p))], [.. final.Keys.Where(BriefPaths.Denied)], cut, truncated));
    }
}
