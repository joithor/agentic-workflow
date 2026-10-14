using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>One repo's refresh: counts and codes only, never paths' contents.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The commit it now reflects, or null when it couldn't be read.</param>
/// <param name="Added">Documents indexed anew.</param>
/// <param name="Removed">Documents dropped (gone from HEAD or from the globs).</param>
/// <param name="Kept">Documents unchanged.</param>
/// <param name="Blocked">Documents the scrubber withheld (indexed without text).</param>
/// <param name="Code">The error code when the repo couldn't be refreshed, else null.</param>
public sealed record RepoRefresh(ContextRepoName Repo, SourceVersion? Head, int Added, int Removed, int Kept, int Blocked, string? Code)
{
    /// <summary>Documents the globs match but that weren't indexed: over the size cap, or unreadable this run (their earlier rows are kept).</summary>
    public int Skipped { get; init; }

    /// <summary>
    /// Documents whose blob didn't change but that were stored under another scrub ruleset, so were cleaned again and
    /// indexed with text (so they are among <see cref="Added"/>); one the new rules block or one over the size cap counts
    /// as <see cref="Blocked"/> or <see cref="Skipped"/> instead.
    /// </summary>
    public int Recleaned { get; init; }
}
