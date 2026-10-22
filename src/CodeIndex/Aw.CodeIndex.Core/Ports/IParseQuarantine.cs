using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// The crash guard around parsing (spec §9 CI12). A stack overflow cannot be caught and a thread cannot be aborted, so the
/// build marks the blob it is about to parse and clears the mark after; a start that finds a mark knows the last run died
/// on that blob and quarantines it (it is then parsed as 'parsed = 0', 'too-deep' without being read by a parser), so
/// hostile files, a build that never publishes and an unrelated kill all converge instead of crash-looping. The list is
/// durable, survives a parser-id change, is kept until the blob is no longer in the head, and holds at most
/// <see cref="CodeIndexRules.MaxQuarantined"/> blobs (the oldest go first). A disk failure throws an I/O exception:
/// a build that cannot protect itself must fail, not parse unguarded.
/// </summary>
public interface IParseQuarantine
{
    /// <summary>Called once at the start of a build: a mark left by a run that died is moved onto the list.</summary>
    /// <param name="repo">The repo.</param>
    /// <returns>How many blobs the list holds now.</returns>
    public int Recover(RepoName repo);

    /// <summary>Whether a blob is on the list.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="blob">The git object id.</param>
    /// <returns>True when the file must not be given to a parser.</returns>
    public bool Contains(RepoName repo, string blob);

    /// <summary>Marks a blob as about to be parsed (written and flushed before the parse).</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="blob">The git object id.</param>
    public void Begin(RepoName repo, string blob);

    /// <summary>Clears the mark after the parse returned.</summary>
    /// <param name="repo">The repo.</param>
    public void End(RepoName repo);

    /// <summary>Drops the blobs that are no longer in the head.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="blobsInHead">Every blob id of the snapshot being indexed.</param>
    /// <returns>How many blobs the list holds now.</returns>
    public int Keep(RepoName repo, IReadOnlySet<string> blobsInHead);
}
