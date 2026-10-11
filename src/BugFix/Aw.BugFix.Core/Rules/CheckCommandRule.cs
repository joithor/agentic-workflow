using System.Text.RegularExpressions;

namespace Aw.BugFix.Core.Rules;

/// <summary>A test command must execute the check: no non-runner (grep, cat, true, …) and no interpreter given inline code.</summary>
public static partial class CheckCommandRule
{
    private static readonly HashSet<string> _interpreters = new(StringComparer.Ordinal)
    {
        "sh", "bash", "zsh", "dash", "ksh", "fish", "node", "deno", "bun", "python", "python3", "ruby", "perl", "php",
    };

    // Programs that run another program given as arguments: the real command would hide behind them.
    private static readonly HashSet<string> _wrappers = new(StringComparer.Ordinal)
    {
        "env", "xargs", "timeout", "nice", "nohup", "command", "exec", "sudo", "doas", "stdbuf", "time", "chronic", "unbuffer", "script",
    };

    private static readonly HashSet<string> _packageRunners = new(StringComparer.Ordinal) { "npx", "pnpx", "bunx" };

    private static readonly HashSet<string> _callFlags = new(StringComparer.Ordinal) { "-c", "--call" };

    private static readonly HashSet<string> _inlineLongFlags = new(StringComparer.Ordinal) { "--eval", "--command", "--print" };

    private static readonly HashSet<string> _nonRunners = new(StringComparer.Ordinal)
    {
        "grep", "egrep", "fgrep", "rg", "cat", "test", "[", "true", "false", "echo", "printf", "ls", "stat", "find", "diff",
        "cmp", "head", "tail", "wc",
    };

    /// <summary>Why the command could pass without running the check, or null when it may run.</summary>
    /// <param name="command">The argv (at least one element).</param>
    /// <returns>The reason, or null.</returns>
    public static string? Refusal(IReadOnlyList<string> command)
    {
        try
        {
            return Classify(command);
        }
        catch (RegexMatchTimeoutException)
        {
            return "the command could not be classified in time";   // a timeout never lets a command through
        }
    }

    private static string? Classify(IReadOnlyList<string> command)
    {
        var program = command[0][(command[0].LastIndexOf('/') + 1)..];
        if (_nonRunners.Contains(program) || _wrappers.Contains(program))
        {
            return $"the command must execute the check, not `{program}`";
        }

        var inline = InlineArgument(program, [.. command.Skip(1)]);
        return inline is null ? null : $"the command must execute the check, not inline code (`{program} {inline}`)";
    }

    // The argument that makes the program run inline code instead of a file, or null.
    private static string? InlineArgument(string program, List<string> args)
    {
        if (_packageRunners.Contains(program))
        {
            return args.Find(static a => _callFlags.Contains(a));
        }

        if (!_interpreters.Contains(program) && !VersionedPython.IsMatch(program))
        {
            return null;
        }

        if (string.Equals(program, "deno", StringComparison.Ordinal) && args.Count > 0 && string.Equals(args[0], "eval", StringComparison.Ordinal))
        {
            return "eval";
        }

        return args.Find(a => Inline(program, a));
    }

    // -c/-e/-E style flags everywhere; node's -p/--print and php's -r also run inline code.
    private static bool Inline(string program, string arg) =>
        _inlineLongFlags.Contains(arg)
        || InlineFlag.IsMatch(arg)
        || (string.Equals(program, "node", StringComparison.Ordinal) && PrintFlag.IsMatch(arg))
        || (string.Equals(program, "php", StringComparison.Ordinal) && string.Equals(arg, "-r", StringComparison.Ordinal));

    [GeneratedRegex("^python[0-9]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionedPython { get; }

    [GeneratedRegex("^-[a-zA-Z]*[ceE]\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineFlag { get; }

    [GeneratedRegex("^-[a-zA-Z]*p\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrintFlag { get; }
}
