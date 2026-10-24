namespace Aw.Context.Core.Engine;

/// <summary>
/// The shared file roles every repo has (S-4): tests, fixtures, demos, icons, deprecated trees, shells, routers and crons. A file in
/// one is never the implementer of a feature; the profile's 'index.roles' names the roles a repo does not index at all. '**/' is any
/// directories, '*' stays inside one segment, matching ignores case, and a glob that matches a directory covers what is under it.
/// </summary>
public static class SharedRoles
{
    /// <summary>The globs of the default roles.</summary>
    public static IReadOnlyList<string> Defaults { get; } =
    [
        "**/__tests__/**",
        "**/*.test.*",
        "**/*.spec.*",
        "**/*fixture*",
        "**/demo/**",
        "**/icons/**",
        "**/*deprecated*/**",
        "**/*shell*",
        "**/*router*",
        "**/*cron*",
    ];
}
