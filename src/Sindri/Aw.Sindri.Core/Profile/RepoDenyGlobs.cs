namespace Aw.Sindri.Core.Profile;

/// <summary>The deny globs that apply to one repo: the profile's <c xml:lang="en">index.denyPaths</c> and the repo's own, which the context engine adds to its built-in secret globs.</summary>
public static class RepoDenyGlobs
{
    /// <summary>The globs of <paramref name="repo"/> under <paramref name="profile"/>.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="repo">One of its repos.</param>
    /// <returns>The globs, or null when neither names any.</returns>
    public static IReadOnlyList<string>? For(SindriProfile profile, RepoEntry repo)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(repo);
        string[] globs = [.. profile.Index?.DenyPaths ?? [], .. repo.Index?.DenyPaths ?? []];
        return globs.Length == 0 ? null : globs;
    }
}
