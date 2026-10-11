using System.Text.RegularExpressions;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// A test command must execute the check: no non-runner (grep, cat, true, …), no program that runs another one (env,
/// timeout, …), and no interpreter given inline code. A package runner (npx, npm exec, uv run, …) is looked through: the
/// program it runs is classified the same way.
/// </summary>
public static partial class CheckCommandRule
{
    // Programs that run another program given as arguments: the real command would hide behind them.
    private static readonly HashSet<string> _wrappers = new(StringComparer.Ordinal)
    {
        "env", "xargs", "timeout", "nice", "nohup", "command", "exec", "sudo", "doas", "stdbuf", "time", "chronic", "unbuffer", "script",
    };

    private static readonly HashSet<string> _nonRunners = new(StringComparer.Ordinal)
    {
        "grep", "egrep", "fgrep", "rg", "cat", "test", "[", "true", "false", "echo", "printf", "ls", "stat", "find", "diff",
        "cmp", "head", "tail", "wc",
    };

    // Package runners: program → the words before the program they run (e.g. "npm exec <program>").
    private static readonly Dictionary<string, string[]> _runners = new(StringComparer.Ordinal)
    {
        ["npx"] = [],
        ["pnpx"] = [],
        ["bunx"] = [],
        ["npm"] = ["exec"],
        ["pnpm"] = ["exec", "dlx"],
        ["yarn"] = ["exec", "dlx"],
        ["bun"] = ["x"],
        ["uv"] = ["run"],
        ["poetry"] = ["run"],
        ["bundle"] = ["exec"],
    };

    private static readonly HashSet<string> _callFlags = new(StringComparer.Ordinal) { "-c", "--call" };

    private static readonly HashSet<string> _inlineLongFlags = new(StringComparer.Ordinal) { "--eval", "--print", "--command" };

    // The inline-code flags of each interpreter: shells -c, node -e/-p, perl -e/-E, ruby -e, php -r, python -c.
    private static readonly Dictionary<string, Regex> _inlineFlags = new(StringComparer.Ordinal)
    {
        ["sh"] = ShellFlag,
        ["bash"] = ShellFlag,
        ["zsh"] = ShellFlag,
        ["dash"] = ShellFlag,
        ["ksh"] = ShellFlag,
        ["fish"] = ShellFlag,
        ["node"] = NodeFlag,
        ["deno"] = NodeFlag,
        ["bun"] = NodeFlag,
        ["perl"] = PerlFlag,
        ["ruby"] = PerlFlag,
        ["php"] = PhpFlag,
    };

    /// <summary>Why the command could pass without running the check, or null when it may run.</summary>
    /// <param name="command">The argv (at least one element).</param>
    /// <returns>The reason, or null.</returns>
    public static string? Refusal(IReadOnlyList<string> command)
    {
        try
        {
            return Classify(command, depth: 0);
        }
        catch (RegexMatchTimeoutException)
        {
            return "the command could not be classified in time";   // a timeout never lets a command through
        }
    }

    private static string? Classify(IReadOnlyList<string> command, int depth)
    {
        var program = command[0][(command[0].LastIndexOf('/') + 1)..];
        if (_nonRunners.Contains(program) || _wrappers.Contains(program))
        {
            return $"the command must execute the check, not `{program}`";
        }

        var args = command.Skip(1).ToList();
        if (_runners.TryGetValue(program, out var verbs))
        {
            return Runner(program, verbs, args, depth);
        }

        var inline = InlineArgument(program, args);
        return inline is null ? null : $"the command must execute the check, not inline code (`{program} {inline}`)";
    }

    // Look through the runner to the program it runs: npx node -e … is node -e ….
    private static string? Runner(string program, string[] verbs, List<string> args, int depth)
    {
        if (verbs.Length > 0 && (args.Count == 0 || !verbs.Contains(args[0], StringComparer.Ordinal)))
        {
            return null;   // not the runner form (e.g. npm test): the package's own script runs
        }

        var rest = args.Skip(verbs.Length > 0 ? 1 : 0).ToList();
        if (rest.TakeWhile(static a => a.StartsWith('-')).FirstOrDefault(static a => _callFlags.Contains(a)) is { } call)
        {
            return $"the command must execute the check, not inline code (`{program} {call}`)";
        }

        var target = rest.SkipWhile(static a => a.StartsWith('-')).ToList();
        return target.Count == 0 || depth > 3 ? null : Classify(target, depth + 1);
    }

    // The argument that makes an interpreter run inline code instead of a file (deno eval, or the interpreter's flags).
    private static string? InlineArgument(string program, List<string> args)
    {
        if (string.Equals(program, "deno", StringComparison.Ordinal) && args.Count > 0 && string.Equals(args[0], "eval", StringComparison.Ordinal))
        {
            return "eval";
        }

        var flags = _inlineFlags.GetValueOrDefault(program) ?? (VersionedPython.IsMatch(program) ? PythonFlag : null);
        return flags is null ? null : args.Find(a => flags.IsMatch(a) || _inlineLongFlags.Contains(a));
    }

    [GeneratedRegex("^python[0-9.]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionedPython { get; }

    [GeneratedRegex("^-[a-zA-Z]*c[a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ShellFlag { get; }

    [GeneratedRegex("^-[a-zA-Z]*[ep][a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NodeFlag { get; }

    [GeneratedRegex("^-[a-zA-Z]*[eE][a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PerlFlag { get; }

    [GeneratedRegex("^-[a-zA-Z]*r[a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhpFlag { get; }

    [GeneratedRegex("^-[a-zA-Z]*c[a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PythonFlag { get; }
}
