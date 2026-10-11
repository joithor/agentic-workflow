namespace Aw.BugFix.Core.Rules;

/// <summary>
/// The paths a test command names besides its program: whole arguments, values after "=" (--require=./x.cjs,
/// --config=vt.ts), values attached to a short flag (-r./x.js), and Python modules named with -m or -p (a.b → a/b.py,
/// a/b/__init__.py). The repository files among them are protected like the check.
/// </summary>
public static class CommandFilesRule
{
    private static readonly HashSet<string> _moduleFlags = new(StringComparer.Ordinal) { "-m", "-p" };

    private static readonly string[] _scriptExtensions =
    [
        ".sh", ".bash", ".js", ".cjs", ".mjs", ".ts", ".cts", ".mts", ".jsx", ".tsx", ".py", ".rb", ".pl", ".php", ".json", ".toml",
        ".yaml", ".yml", ".ini", ".cfg",
    ];

    /// <summary>Every path the command's arguments name, as written (relative to the run's directory or full).</summary>
    /// <param name="command">The argv.</param>
    /// <returns>The candidate paths; a module's two possible files are optional, everything else must exist if it looks like a file.</returns>
    public static IReadOnlyList<(string Path, bool Optional)> Paths(IReadOnlyList<string> command)
    {
        var paths = new List<(string Path, bool Optional)>();
        for (var i = 1; i < command.Count; i++)
        {
            var arg = command[i];
            paths.Add((arg, false));
            if (arg.StartsWith('-') && arg.IndexOf('=', StringComparison.Ordinal) is var eq and > 0)
            {
                paths.Add((arg[(eq + 1)..], false));
            }
            else if (arg.Length > 2 && arg[0] == '-' && arg[1] != '-')
            {
                paths.Add((arg[2..], true));   // -xVALUE may be a short flag cluster, not a path
            }

            if (_moduleFlags.Contains(arg) && i + 1 < command.Count)
            {
                var module = command[i + 1].Replace('.', '/');
                paths.Add(($"{module}.py", true));
                paths.Add(($"{module}/__init__.py", true));
            }
        }

        return paths;
    }

    /// <summary>Whether <paramref name="path"/> looks like a script or config file the command would load.</summary>
    /// <param name="path">A path as written.</param>
    /// <returns>True for a known script or config extension.</returns>
    public static bool LooksLikeFile(string path) =>
        _scriptExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}
