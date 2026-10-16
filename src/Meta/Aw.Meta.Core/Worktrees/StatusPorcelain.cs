namespace Aw.Meta.Core.Worktrees;

/// <summary>Reads "git status --porcelain --untracked-files=all", leaving out untracked build output.</summary>
internal static class StatusPorcelain
{
    private const string Untracked = "??";
    private const string RenameArrow = " -> ";

    /// <summary>The entries that count as work: everything except an untracked file under a directory named like a build output.</summary>
    /// <param name="output">The porcelain output.</param>
    /// <param name="buildOutputs">Directory names (bin, obj, node_modules) whose untracked files are ignored.</param>
    /// <returns>The paths that keep a worktree.</returns>
    internal static IReadOnlyList<string> Significant(string output, IReadOnlyList<string> buildOutputs)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(buildOutputs);
        List<string> found = [];
        foreach (var line in output.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 2)
            {
                continue;
            }

            var untracked = line.StartsWith(Untracked, StringComparison.Ordinal);
            var path = Unquote(Path(line));
            if (!(untracked && InBuildOutput(path, buildOutputs)))
            {
                found.Add(path);
            }
        }

        return found;
    }

    // "XY path": the status is two characters and a space; a trimmed first line may have lost its leading space.
    private static string Path(string line)
    {
        var path = line.Length >= 3 && line[2] == ' ' ? line[3..] : line[1] == ' ' ? line[2..] : line[3..];
        var arrow = path.IndexOf(RenameArrow, StringComparison.Ordinal);
        return arrow >= 0 ? path[(arrow + RenameArrow.Length)..] : path;
    }

    private static string Unquote(string path) => path.Length >= 2 && path[0] == '"' && path[^1] == '"' ? path[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal) : path;

    // A build-output name counts as a directory: any segment but the last, or the last when the path ends in a slash.
    private static bool InBuildOutput(string path, IReadOnlyList<string> buildOutputs)
    {
        var segments = path.Split('/');
        var directories = path.EndsWith('/') ? segments : segments[..^1];
        return directories.Any(segment => buildOutputs.Contains(segment, StringComparer.Ordinal));
    }
}
