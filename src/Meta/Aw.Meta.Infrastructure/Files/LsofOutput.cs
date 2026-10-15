using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Reads the field output of "lsof -F pcn" (the macOS fallback of the process scan).</summary>
internal static class LsofOutput
{
    /// <summary>Groups the p (pid), c (command) and n (name) fields into one location per process.</summary>
    /// <param name="output">The lsof output.</param>
    /// <returns>The processes with the absolute paths they hold.</returns>
    internal static IReadOnlyList<ProcessLocation> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        List<ProcessLocation> found = [];
        var (pid, command, paths) = (0, string.Empty, new List<string>());
        foreach (var line in output.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (line[0])
            {
                case 'p' when int.TryParse(line.AsSpan(1), out var next):
                    Flush();
                    (pid, command, paths) = (next, string.Empty, []);
                    break;
                case 'c':
                    command = line[1..];
                    break;
                case 'n' when pid > 0 && line.Length > 1 && line[1] == '/':
                    paths.Add(line[1..]);
                    break;
                default:
                    break;
            }
        }

        Flush();
        return found;

        void Flush()
        {
            if (pid > 0)
            {
                found.Add(new ProcessLocation(pid, command, paths));
            }
        }
    }
}
