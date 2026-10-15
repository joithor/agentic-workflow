namespace Aw.Context.Core.Engine;

/// <summary>
/// The paths a repo's extraction never reads (S-4, the profile's 'index.denyPaths' and the built-in secret globs): they never
/// reach a recognizer, the clustering snapshot or the labeler. Globs are repo-relative with '/' separators, matched without
/// regard to case: '**/' is any directories, a trailing '**' is anything, '*' stays inside one segment, '?' is one character.
/// </summary>
public static class PathDenylist
{
    /// <summary>The secret globs every repo has.</summary>
    public static IReadOnlyList<string> Defaults { get; } =
    [
        ".env*", "**/.env*", "**/*.pem", "**/*.key", "**/*.p12", "**/*.pfx", "**/id_rsa*", "**/*.tfstate", "**/*.tfvars",
        "**/credentials*", "**/.npmrc", "**/.netrc", "**/secrets/**",
    ];

    /// <summary>Whether a path matches any glob.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="globs">The globs.</param>
    /// <returns>True when the path is denied.</returns>
    public static bool Matches(string path, IReadOnlyList<string> globs) => throw new NotSupportedException("S-4: PathDenylist is not built yet");
}
