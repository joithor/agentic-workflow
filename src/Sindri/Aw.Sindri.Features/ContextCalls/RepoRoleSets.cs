using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>A repo's roles as the context engine counts them: every role of <c xml:lang="en">index.roles</c> with its globs and whether <c xml:lang="en">index.demote</c> names it (the others are ignored).</summary>
internal static class RepoRoleSets
{
    /// <summary>The roles of <paramref name="repo"/>.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="repo">One of its repos.</param>
    /// <returns>One entry per role, in name order; or null when the repo has no roles.</returns>
    internal static IReadOnlyList<RepoRole>? For(SindriProfile profile, RepoEntry repo) => throw new NotSupportedException();
}
