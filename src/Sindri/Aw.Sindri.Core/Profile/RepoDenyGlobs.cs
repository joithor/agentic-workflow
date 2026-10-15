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

    /// <summary>Every repo of <paramref name="profile"/> with its globs: the one mapping from the profile to the scope the context engine reads (every surface and the doctor use it).</summary>
    /// <param name="profile">The approved profile.</param>
    /// <returns>Each repo's name, path and globs (null when none), in profile order.</returns>
    public static IReadOnlyList<(string Name, string Path, IReadOnlyList<string>? DenyPaths)> Entries(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return [.. (profile.Repos ?? []).Select(r => (r.Name, r.Path, For(profile, r)))];
    }
}
