namespace Aw.BugFix.Core.Rules;

/// <summary>
/// The paths a test command names besides its program: whole arguments, values after "=" (--require=./x.cjs,
/// --config=vt.ts), values attached to a short flag (-r./x.js), and Python modules named with -m or -p (a.b → a/b.py,
/// a/b/__init__.py). The repository files among them are protected like the check; a required one that doesn't exist
/// refuses the baseline, since a fix could add it.
/// </summary>
public static class CommandFilesRule
{
    private static readonly HashSet<string> _moduleFlags = new(StringComparer.Ordinal) { "-m", "-p" };

    private static readonly string[] _scriptExtensions =
    [
        ".sh", ".bash", ".js", ".cjs", ".mjs", ".ts", ".cts", ".mts", ".jsx", ".tsx", ".py", ".rb", ".pl", ".php", ".json", ".toml",
        ".yaml", ".yml", ".ini", ".cfg",
    ];

    // What a module loader tries for a specifier without an extension (node, python, ruby).
    private static readonly string[] _resolverSuffixes =
    [
        ".js", ".cjs", ".mjs", ".ts", ".cts", ".mts", ".json", ".py", ".rb", "/index.js", "/index.cjs", "/index.mjs", "/index.ts", "/__init__.py",
    ];

    /// <summary>Every path the command's arguments name, as written (relative to the run's directory or full).</summary>
    /// <param name="command">The argv.</param>
    /// <returns>Each path and whether it must exist (a script, config or path-like value; not a module guess or a pattern).</returns>
    public static IReadOnlyList<(string Path, bool Required)> Paths(IReadOnlyList<string> command)
    {
        var paths = new List<(string Path, bool Required)>();
        for (var i = 1; i < command.Count; i++)
        {
            var arg = command[i];
            paths.Add((arg, Required(arg)));
            if (arg.StartsWith('-') && arg.IndexOf('=', StringComparison.Ordinal) is var eq and > 0)
            {
                paths.Add((arg[(eq + 1)..], Required(arg[(eq + 1)..])));
            }
            else if (arg.Length > 2 && arg[0] == '-' && arg[1] != '-')
            {
                // -xVALUE: a path only if it looks like one (-r./x.js), not a short-flag cluster (-xvf).
                paths.Add((arg[2..], arg.Contains('/', StringComparison.Ordinal) && Required(arg[2..])));
            }

            if (_moduleFlags.Contains(arg) && i + 1 < command.Count)
            {
                var module = command[i + 1].Replace('.', '/');
                paths.Add(($"{module}.py", false));
                paths.Add(($"{module}/__init__.py", false));
            }
        }

        return paths;
    }

    /// <summary>The files a loader would try for <paramref name="path"/>: the path itself, then the resolver suffixes.</summary>
    /// <param name="path">A path as written.</param>
    /// <returns>The candidates, in loader order.</returns>
    public static IEnumerable<string> LoaderCandidates(string path) =>
        HasExtension(path) ? [path] : [path, .. _resolverSuffixes.Select(s => path.TrimEnd('/') + s)];

    // A script or config by its extension, or anything written as a path (contains '/' or starts with '.'), but not a
    // flag, a glob or a package pattern (go's ./...), which name sets of files rather than one.
    private static bool Required(string path) =>
        !path.StartsWith('-')
        && !path.Contains("...", StringComparison.Ordinal)
        && path.IndexOfAny(['*', '?', '[']) < 0
        && (HasExtension(path) || path.Contains('/', StringComparison.Ordinal) || path.StartsWith('.'));

    private static bool HasExtension(string path) =>
        _scriptExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}
