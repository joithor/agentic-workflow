using System.Text.RegularExpressions;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// A test command must execute the check: no non-runner (grep, cat, true, …), no program that runs another one (env,
/// timeout, …), and no interpreter given inline code. A package runner (npx, npm exec, uv run, …) is looked through: the
/// program it runs is classified the same way, and only options known not to hide or change it may come before it.
/// </summary>
public static class CheckCommandRule
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

    // Package runners: program → the words before the program they run (e.g. "npm exec <program>", "uv tool run <program>").
    private static readonly Dictionary<string, string[]> _runners = new(StringComparer.Ordinal)
    {
        ["npx"] = [],
        ["pnpx"] = [],
        ["bunx"] = [],
        ["uvx"] = [],
        ["npm"] = ["exec", "x"],
        ["pnpm"] = ["exec", "dlx"],
        ["yarn"] = ["exec", "dlx", "node"],
        ["bun"] = ["x"],
        ["uv"] = ["run", "tool run"],
        ["poetry"] = ["run"],
        ["bundle"] = ["exec"],
    };

    // Runners whose verb is itself the program they run: yarn node … is node ….
    private static readonly HashSet<string> _programVerbs = new(StringComparer.Ordinal) { "node" };

    // Subcommands that run the package's own scripts (npm test, pnpm run lint): not a program on PATH.
    private static readonly HashSet<string> _scriptVerbs = new(StringComparer.Ordinal) { "test", "t", "tst", "run", "run-script" };

    // The runner options that may come before the program. Any other option is refused: an unknown one could take the
    // next argument as its value and hide the real program behind it, and some change what runs (--node-options,
    // --env-file, --call, --shell-mode, another directory).
    private static readonly HashSet<string> _runnerFlags = new(StringComparer.Ordinal)
    {
        "-y", "--yes", "--no", "-q", "--quiet", "-s", "--silent", "--offline", "--prefer-offline", "--no-install", "--bun", "--frozen",
        "--locked", "--no-sync", "--isolated", "--all-extras", "--no-dev",
    };

    // Runner options whose value is the next argument (unless written --name=value): a package, an interpreter, a
    // workspace package (its directory is still under the frozen run's), or uv run's module.
    private static readonly HashSet<string> _runnerValueOptions = new(StringComparer.Ordinal)
    {
        "-p", "--package", "--with", "--python", "--extra", "--group", "--filter", "-F", "--workspace", "-w", "-m", "--module",
    };

    private static readonly HashSet<string> _callFlags = new(StringComparer.Ordinal) { "-c", "--call", "--shell-mode" };

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

        var inline = InlineCode.Argument(program, args);
        return inline is null ? null : $"the command must execute the check, not inline code (`{program} {inline}`)";
    }

    // Look through the runner to the program it runs: npx node -e … is node -e …. The first argument past the runner's
    // verb and options is classified fully; every later one is checked for inline code too.
    private static string? Runner(string program, string[] verbs, List<string> args, int depth)
    {
        var (at, refusal) = Options(program, args, 0, interpreterFlags: true);
        if (refusal is not null || verbs.Length == 0)
        {
            return refusal ?? Target(args[at..], depth);
        }

        var verb = Verb(verbs, args, at);
        if (verb < 0)
        {
            return NotRunnerForm(program, args, at, depth);
        }

        if (_programVerbs.Contains(args[verb]))
        {
            return Target(args[verb..], depth);
        }

        (at, refusal) = Options(program, args, verb + 1, interpreterFlags: false);   // bun x -p is --package, bun -p is --print
        return refusal ?? Target(args[at..], depth);
    }

    // npm test runs the package's own script; pnpm grep … runs grep; bun -e … is inline code.
    private static string? NotRunnerForm(string program, List<string> args, int at, int depth) =>
        InlineCode.Argument(program, args) is { } inline
            ? $"the command must execute the check, not inline code (`{program} {inline}`)"
            : at < args.Count && !_scriptVerbs.Contains(args[at]) ? Target(args[at..], depth) : null;

    // The index of the verb's last word at args[at], or -1.
    private static int Verb(string[] verbs, List<string> args, int at)
    {
        foreach (var verb in verbs)
        {
            var words = verb.Split(' ');
            if (args.Skip(at).Take(words.Length).SequenceEqual(words, StringComparer.Ordinal))
            {
                return at + words.Length - 1;
            }
        }

        return -1;
    }

    // Skips the runner's options from args[from]: the index of the first argument that isn't one, or why the command is refused.
    private static (int Next, string? Refusal) Options(string program, List<string> args, int from, bool interpreterFlags)
    {
        for (var i = from; i < args.Count; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--", StringComparison.Ordinal) || !arg.StartsWith('-'))
            {
                return (string.Equals(arg, "--", StringComparison.Ordinal) ? i + 1 : i, null);
            }

            var name = arg.Split('=', 2)[0];
            if (OptionRefusal(program, arg, name, interpreterFlags) is { } refusal)
            {
                return (i, refusal);
            }

            i += _runnerValueOptions.Contains(name) && !arg.Contains('=', StringComparison.Ordinal) ? 1 : 0;
        }

        return (args.Count, null);
    }

    // An option no runner form may have before the program: inline code (bun -p is --print, not --package), or one not known to be harmless.
    private static string? OptionRefusal(string program, string arg, string name, bool interpreterFlags)
    {
        if (_callFlags.Contains(name) || (interpreterFlags && InlineCode.Argument(program, [arg]) is not null))
        {
            return $"the command must execute the check, not inline code (`{program} {name}`)";
        }

        return _runnerValueOptions.Contains(name) || _runnerFlags.Contains(name)
            ? null
            : $"`{program}` option `{name}` hides which program runs; put it after the program or in the runner's config, or run from the package's directory with --cwd";
    }

    private static string? Target(List<string> rest, int depth)
    {
        if (rest.Count == 0)
        {
            return null;
        }

        return depth > 3 ? "the command nests runners too deeply to see which program runs" : Classify(rest, depth + 1) ?? LaterInline(rest, 0);
    }

    private static string? LaterInline(List<string> rest, int first)
    {
        for (var j = first + 1; j < rest.Count; j++)
        {
            var program = rest[j][(rest[j].LastIndexOf('/') + 1)..];
            if (!rest[j].StartsWith('-') && InlineCode.Argument(program, rest[(j + 1)..]) is { } inline)
            {
                return $"the command must execute the check, not inline code (`{program} {inline}`)";
            }
        }

        return null;
    }
}
