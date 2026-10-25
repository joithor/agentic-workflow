using Aw.Context.Core.Map;

namespace Aw.Context.Core.Roles;

/// <summary>Doctor 'context:roles': warns when indexed files look like a role but match no glob.</summary>
public static class RoleHealth
{
    /// <summary>The check for the role counts of a map.</summary>
    /// <param name="roles">The per-repo counts.</param>
    /// <returns>Ok when no repo has an unassigned file; a warning naming the counts and the fix otherwise.</returns>
    public static MapHealth Check(IReadOnlyList<RepoRoles> roles) => throw new NotSupportedException();
}
