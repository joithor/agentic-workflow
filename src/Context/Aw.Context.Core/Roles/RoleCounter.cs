using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Roles;

/// <summary>Counts a map's code files per role per repo and finds the role-like ones no glob covers.</summary>
public static class RoleCounter
{
    /// <summary>The repo-relative paths of the map's code nodes, by repo name (the repo <see cref="NodeRepos"/> puts each in).</summary>
    /// <param name="graph">The map as the caller sees it.</param>
    /// <param name="mapRepo">The repo the map version was built for.</param>
    /// <returns>Paths per repo.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> CodePaths(GraphSnapshot graph, string? mapRepo) => throw new NotSupportedException();

    /// <summary>The role counts of every repo that configures a role or has an unassigned role-like file.</summary>
    /// <param name="repos">The repos in scope.</param>
    /// <param name="pathsByRepo">The indexed paths per repo name.</param>
    /// <returns>One entry per such repo, in the order of <paramref name="repos"/>.</returns>
    public static IReadOnlyList<RepoRoles> Of(IReadOnlyList<ContextRepo> repos, IReadOnlyDictionary<string, IReadOnlyList<string>> pathsByRepo) => throw new NotSupportedException();
}
