namespace Aw.Context.Infrastructure.Map;

/// <summary>Finds the git worktree a path is in (spec §3: nothing Context writes or reads as a map lands in one; SND-CONTEXT-005).</summary>
internal static class Worktrees
{
    /// <summary>The root of the git worktree that holds <paramref name="path"/>, or null.</summary>
    /// <param name="path">An absolute path; it need not exist.</param>
    /// <returns>The worktree root (the folder with '.git'), or null when the path is in none.</returns>
    internal static string? Containing(string path)
    {
        for (var dir = Path.GetDirectoryName(Path.GetFullPath(path)); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var git = Path.Combine(dir, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                return dir;
            }
        }

        return null;
    }
}
