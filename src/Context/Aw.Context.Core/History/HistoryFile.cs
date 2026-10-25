using Aw.Context.Core.Engine;

namespace Aw.Context.Core.History;

/// <summary>A file that implements a node of the scope: what the git step searches.</summary>
/// <param name="Repo">The repo its edge evidence names, or null when it names none (counted as unlocated).</param>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Anchors">The anchors whose scope the file implements.</param>
public sealed record HistoryFile(ContextRepoName? Repo, string Path, IReadOnlyList<string> Anchors);
