using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Control;

/// <summary>The pure half of repo resolution (API spec §3.1): which repo entry contains a main checkout.</summary>
public static class RepoScope
{
    /// <summary>The entry whose path contains <paramref name="checkout"/> (equal to it, or a parent of it); the longest path wins.</summary>
    /// <param name="checkout">The main checkout's real path (absolute, '/'-separated).</param>
    /// <param name="repos">The repo entries, their paths already real.</param>
    /// <returns>The entry's name, or null when none contains it (or its name isn't a valid repo name).</returns>
    public static RepoName? Longest(string checkout, IReadOnlyList<RepoEntry> repos) =>
        throw new NotSupportedException("red: the longest containing repo entry");
}
