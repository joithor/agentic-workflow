using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>The commit an index describes, from stored state only (no git, no live probe).</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">The commit, or null when the file is missing, damaged or of another schema (no recorded head, so it rebuilds).</param>
public sealed record RecordedHead(RepoName Repo, CommitSha? Commit);
