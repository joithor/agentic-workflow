using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Control;

/// <summary>The pure half of repo resolution (API spec §3.1): which repo entry contains a main checkout.</summary>
public static class RepoScope
{
    /// <summary>
    /// The entry whose path contains <paramref name="checkout"/> (equal to it, or a parent of it by whole path segments,
    /// so '/work/demo' doesn't contain '/work/demolition'); the longest path wins.
    /// </summary>
    /// <param name="checkout">The main checkout's real path (absolute, '/'-separated).</param>
    /// <param name="repos">The repo entries, their paths already real.</param>
    /// <returns>The entry's name, or null when none contains it (an entry whose name isn't a valid repo name is skipped).</returns>
    public static RepoName? Longest(string checkout, IReadOnlyList<RepoEntry> repos)
    {
        var target = Trimmed(checkout);
        RepoName? best = null;
        var longest = -1;
        foreach (var repo in repos)
        {
            var path = Trimmed(repo.Path);
            if (path.Length > longest && Contains(path, target) && RepoName.TryFrom(repo.Name, out var name))
            {
                best = name;
                longest = path.Length;
            }
        }

        return best;
    }

    private static string Trimmed(string path) => path.Length > 1 ? path.TrimEnd('/') : path;

    private static bool Contains(string parent, string path) =>
        string.Equals(parent, path, StringComparison.Ordinal)
        || path.StartsWith(parent.EndsWith('/') ? parent : parent + "/", StringComparison.Ordinal);
}
