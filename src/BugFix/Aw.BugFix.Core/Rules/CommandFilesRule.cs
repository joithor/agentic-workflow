namespace Aw.BugFix.Core.Rules;

/// <summary>
/// The paths a test command names besides its program, and how each is protected:
/// a positional path (tests/x.sh, ./tools/run) and a script or config file named in an option (--config=vt.ts) must
/// exist; a module specifier (--require=./lib/setup, -r ./x, --import=./y, a positional ./z) and a -m/-p module are
/// protected through every file the loader would try, existing or not, since a fix that adds one the loader tries first
/// changes what runs. Option values that aren't files (URLs, /regexes/, output files, names) are ignored.
/// </summary>
public static class CommandFilesRule
{
    private static readonly HashSet<string> _moduleFlags = new(StringComparer.Ordinal) { "-m", "-p" };

    // Options whose value is a module the runtime loads before the check.
    private static readonly HashSet<string> _loaderOptions = new(StringComparer.Ordinal)
    {
        "--require", "-r", "--import", "--loader", "--experimental-loader", "--setupFiles", "--setup-files", "--preload",
    };

    private static readonly string[] _scriptExtensions =
    [
        ".sh", ".bash", ".js", ".cjs", ".mjs", ".ts", ".cts", ".mts", ".jsx", ".tsx", ".py", ".rb", ".pl", ".php", ".json", ".toml",
        ".yaml", ".yml", ".ini", ".cfg",
    ];

    // What a module loader tries for a specifier without an extension (node, python, ruby), after the path itself.
    private static readonly string[] _resolverSuffixes =
    [
        ".js", ".cjs", ".mjs", ".ts", ".cts", ".mts", ".json", ".py", ".rb", "/index.js", "/index.cjs", "/index.mjs", "/index.ts", "/__init__.py",
    ];

    /// <summary>Every path the command's arguments name (relative to the run's directory or full).</summary>
    /// <param name="command">The argv.</param>
    /// <returns>Each path, whether it must exist, and whether every file the loader would try for it is protected.</returns>
    public static IReadOnlyList<(string Path, bool Required, bool Specifier)> Paths(IReadOnlyList<string> command)
    {
        var paths = new List<(string Path, bool Required, bool Specifier)>();
        for (var i = 1; i < command.Count; i++)
        {
            if (Named(command[i], _loaderOptions.Contains(command[i - 1])) is { } named)
            {
                paths.Add(named);
            }

            if (_moduleFlags.Contains(command[i]) && i + 1 < command.Count)
            {
                paths.AddRange(ModuleFiles(command[i + 1]).Select(static f => (f, false, true)));
            }
        }

        return paths;
    }

    // The path one argument names: a positional path, an option's =value, or a short loader option's attached value.
    private static (string Path, bool Required, bool Specifier)? Named(string arg, bool afterLoaderOption)
    {
        if (!arg.StartsWith('-'))
        {
            return (arg, IsRequiredPath(arg), afterLoaderOption || IsRelativeSpecifier(arg));
        }

        if (arg.IndexOf('=', StringComparison.Ordinal) is var eq and > 0)
        {
            return OptionValue(arg[..eq], arg[(eq + 1)..]);
        }

        return arg.Length > 2 && arg[1] != '-' && _loaderOptions.Contains(arg[..2]) ? (arg[2..], IsPathLike(arg[2..]), true) : null;
    }

    // --config=vt.ts must exist; --require=./x is a specifier; --outputFile=r/j.xml and --base-url=http://… are neither.
    private static (string Path, bool Required, bool Specifier) OptionValue(string option, string value)
    {
        var loaded = _loaderOptions.Contains(option);
        return (value, !IsPattern(value) && (IsFile(value) || (loaded && IsPathLike(value))), loaded);
    }

    private static bool IsRequiredPath(string arg) => IsFile(arg) || (IsPathLike(arg) && !IsPattern(arg));

    /// <summary>The files a loader would try for a specifier: the path itself, then the resolver suffixes.</summary>
    /// <param name="path">A specifier as written.</param>
    /// <returns>The candidates, in loader order.</returns>
    public static IEnumerable<string> LoaderCandidates(string path) =>
        IsFile(path) ? [path] : [path, .. _resolverSuffixes.Select(s => path.TrimEnd('/') + s)];

    // Every file Python could import for a.b.c from the run's directory (first on sys.path under -m), for each prefix.
    private static IEnumerable<string> ModuleFiles(string dotted)
    {
        var parts = dotted.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var n = 1; n <= parts.Length; n++)
        {
            var module = string.Join('/', parts.Take(n));
            yield return $"{module}.py";
            yield return $"{module}/__init__.py";
            yield return $"{module}/__main__.py";
        }
    }

    private static bool IsFile(string path) => !IsPattern(path) && _scriptExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private static bool IsPathLike(string path) => path.Contains('/', StringComparison.Ordinal) || path.StartsWith('.');

    private static bool IsRelativeSpecifier(string path) =>
        (path.StartsWith("./", StringComparison.Ordinal) || path.StartsWith("../", StringComparison.Ordinal)) && !IsPattern(path);

    // Not one file: a URL, a /regex/, a glob, or a package pattern (go's ./...).
    private static bool IsPattern(string path) =>
        path.Contains("://", StringComparison.Ordinal)
        || (path.Length > 1 && path.StartsWith('/') && path.EndsWith('/'))
        || path.Contains("...", StringComparison.Ordinal)
        || path.IndexOfAny(['*', '?', '[']) >= 0;
}
