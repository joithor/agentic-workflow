using System.Text.RegularExpressions;

namespace Aw.BugFix.Core.Rules;

/// <summary>A test command must execute the check: no non-runner (grep, cat, true, …) and no interpreter given inline code.</summary>
public static partial class CheckCommandRule
{
    private static readonly HashSet<string> _interpreters = new(StringComparer.Ordinal)
    {
        "sh", "bash", "zsh", "dash", "ksh", "fish", "node", "deno", "bun", "python", "python3", "ruby", "perl", "php",
    };

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
        var program = command[0][(command[0].LastIndexOf('/') + 1)..];
        if (_nonRunners.Contains(program))
        {
            return $"the command must execute the check, not `{program}`";
        }

        if (!_interpreters.Contains(program) && !VersionedPython.IsMatch(program))
        {
            return null;
        }

        var inline = command.Skip(1).FirstOrDefault(static a => string.Equals(a, "--eval", StringComparison.Ordinal) || string.Equals(a, "--command", StringComparison.Ordinal) || InlineFlag.IsMatch(a));
        return inline is null ? null : $"the command must execute the check, not inline code (`{program} {inline}`)";
    }

    [GeneratedRegex("^python[0-9]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionedPython { get; }

    [GeneratedRegex("^-[a-zA-Z]*[ce]\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineFlag { get; }
}
