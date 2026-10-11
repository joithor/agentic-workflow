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
        ["yarn"] = ["exec", "dlx", "node"],
        ["bun"] = ["x"],
        ["uv"] = ["run"],
        ["poetry"] = ["run"],
        ["bundle"] = ["exec"],
    };

    // Runners whose verb is itself the program they run: yarn node … is node ….
    private static readonly HashSet<string> _programVerbs = new(StringComparer.Ordinal) { "node" };

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
        ["ruby"] = RubyFlag,
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

    // Look through the runner to the program it runs: npx node -e … is node -e …. The first argument past the runner's
    // verb and options is classified fully; every later one is checked for inline code too, so an option value taken
    // for the program (npx --registry r node -e …) can't hide the real one.
    private static string? Runner(string program, string[] verbs, List<string> args, int depth)
    {
        var verbAt = verbs.Length == 0 ? -1 : args.FindIndex(a => verbs.Contains(a, StringComparer.Ordinal));
        if (verbs.Length > 0 && verbAt < 0)
        {
            // Not the runner form: npm test runs the package's own script, but bun -e is still inline code.
            var inline = InlineArgument(program, args);
            return inline is null ? null : $"the command must execute the check, not inline code (`{program} {inline}`)";
        }

        var rest = args.Skip(verbAt >= 0 && _programVerbs.Contains(args[verbAt]) ? verbAt : verbAt + 1).ToList();
        return rest.TakeWhile(static a => a.StartsWith('-')).FirstOrDefault(static a => _callFlags.Contains(a)) is { } call
            ? $"the command must execute the check, not inline code (`{program} {call}`)"
            : Target(rest, depth);
    }

    private static string? Target(List<string> rest, int depth)
    {
        var first = rest.FindIndex(static a => !a.StartsWith('-'));
        return first < 0 || depth > 3 ? null : Classify(rest[first..], depth + 1) ?? LaterInline(rest, first);
    }

    private static string? LaterInline(List<string> rest, int first)
    {
        for (var j = first + 1; j < rest.Count; j++)
        {
            var program = rest[j][(rest[j].LastIndexOf('/') + 1)..];
            if (!rest[j].StartsWith('-') && InlineArgument(program, rest[(j + 1)..]) is { } inline)
            {
                return $"the command must execute the check, not inline code (`{program} {inline}`)";
            }
        }

        return null;
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

    // perl: -e/-E anywhere in a cluster, except clusters that start with a switch whose value is the rest (-Ilib, -Mstrict).
    [GeneratedRegex("^-(?![IMmixF])[a-zA-Z]*[eE][a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PerlFlag { get; }

    // ruby: -e anywhere in a cluster (-E is an encoding), except clusters that start with a switch whose value is the rest (-Itest, -rjson).
    [GeneratedRegex("^-(?![IrCxEF])[a-zA-Z]*e[a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RubyFlag { get; }

    [GeneratedRegex("^-[a-zA-Z]*r[a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhpFlag { get; }

    [GeneratedRegex("^-[a-zA-Z]*c[a-zA-Z]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PythonFlag { get; }
}
