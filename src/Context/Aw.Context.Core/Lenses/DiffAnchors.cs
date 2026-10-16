using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>The pure step of the diff shape: changed files against a map, to their statuses, nodes and the anchors the cascade starts from.</summary>
public static class DiffAnchors
{
    /// <summary>The most changed files a diff answer keeps.</summary>
    public const int MaxFiles = 1_000;

    /// <summary>The most anchors a diff answer starts the cascade from.</summary>
    public const int MaxAnchors = 20;

    /// <summary>Reads each changed path against the map.</summary>
    /// <param name="changes">What the repo changed.</param>
    /// <param name="repo">The repo, with its deny globs.</param>
    /// <param name="graph">The map the caller may read.</param>
    /// <returns>The diff facts and the anchors (at most <see cref="MaxAnchors"/>, confirmed nodes before suggested, then by key).</returns>
    public static (DiffPart Part, IReadOnlyList<GraphNode> Anchors) Build(RepoChanges changes, ContextRepo repo, GraphSnapshot graph) =>
        throw new NotSupportedException();
}
