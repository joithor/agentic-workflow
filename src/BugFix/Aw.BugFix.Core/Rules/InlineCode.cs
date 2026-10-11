using System.Text.RegularExpressions;

namespace Aw.BugFix.Core.Rules;

/// <summary>Whether an interpreter is given code inline instead of a file: shells and python -c, node -e/-p, perl -e/-E, ruby -e, php -r, deno eval.</summary>
internal static partial class InlineCode
{
    // Interpreter options whose value is the next argument, so it isn't taken for the script.
    private static readonly Dictionary<string, string[]> _interpreterValueOptions = new(StringComparer.Ordinal)
    {
        ["python"] = ["-W", "-X"],
        ["node"] = ["-r", "--require", "--import", "--loader", "--experimental-loader", "-C", "--conditions", "--input-type", "--env-file", "--title"],
        ["ruby"] = ["-I", "-r", "-C", "-E", "-F"],
        ["perl"] = ["-I", "-M", "-m"],
        ["shell"] = ["-o", "-O", "+o", "+O"],
    };

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

    // The argument that makes an interpreter run inline code instead of a file (deno eval, or the interpreter's flags).
    // Only the interpreter's own options count: they end at the script, at python's -m module, or at "--"; what follows
    // belongs to the script (pytest -c pytest.ini, unittest -c).
    internal static string? Argument(string program, List<string> args)
    {
        if (string.Equals(program, "deno", StringComparison.Ordinal) && args.Count > 0 && string.Equals(args[0], "eval", StringComparison.Ordinal))
        {
            return "eval";
        }

        var python = VersionedPython.IsMatch(program);
        var flags = _inlineFlags.GetValueOrDefault(program) ?? (python ? PythonFlag : null);
        if (flags is null)
        {
            return null;
        }

        return OwnInline(args, flags, python, _interpreterValueOptions.GetValueOrDefault(python ? "python" : Family(program)) ?? []);
    }

    // The first of the interpreter's own options that is inline code, skipping option values.
    private static string? OwnInline(List<string> args, Regex flags, bool python, string[] valued)
    {
        for (var i = 0; i < args.Count && OwnOption(args[i]); i++)
        {
            if (flags.IsMatch(args[i]) || _inlineLongFlags.Contains(args[i]))
            {
                return args[i];
            }

            if (python && args[i].StartsWith("-m", StringComparison.Ordinal))
            {
                return null;   // the module and its arguments follow
            }

            i += valued.Contains(args[i], StringComparer.Ordinal) ? 1 : 0;
        }

        return null;
    }

    private static bool OwnOption(string arg) => !string.Equals(arg, "--", StringComparison.Ordinal) && (arg.StartsWith('-') || arg.StartsWith('+'));

    private static string Family(string program) => program switch
    {
        "sh" or "bash" or "zsh" or "dash" or "ksh" or "fish" => "shell",
        "deno" or "bun" => "node",
        _ => program,
    };

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
