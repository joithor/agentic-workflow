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
        ["shell"] = ["-o", "-O", "+o", "+O", "--rcfile", "--init-file"],
        ["php"] = ["-d", "-c", "-z"],
    };

    // Long options known to take no value: anything else unknown is assumed to take the next argument as its value.
    private static readonly HashSet<string> _knownLongFlags = new(StringComparer.Ordinal)
    {
        "--test", "--watch", "--no-warnings", "--enable-source-maps", "--trace-warnings", "--expose-gc", "--no-deprecation",
        "--experimental-vm-modules", "--experimental-strip-types", "--test-only", "--inspect", "--inspect-brk", "--trace-uncaught",
        "--norc", "--noprofile", "--posix", "--login",
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
    // Fails closed: after an option not known to take no value, a positional argument is taken for that option's value,
    // not for the script, and the scan goes on (python --check-hash-based-pycs always -c … is still inline code).
    private static string? OwnInline(List<string> args, Regex flags, bool python, string[] valued)
    {
        var ended = true;   // whether the previous option surely took no further argument
        for (var i = 0; i < args.Count && !string.Equals(args[i], "--", StringComparison.Ordinal); i++)
        {
            var arg = args[i];
            if (!OwnOption(arg))
            {
                if (ended)
                {
                    return null;   // the script: what follows is its own
                }

                ended = true;
                continue;
            }

            if (Stop(arg, flags, python) is { } stop)
            {
                return stop.Length == 0 ? null : stop;
            }

            var takesNext = valued.Contains(arg, StringComparer.Ordinal);
            i += takesNext ? 1 : 0;
            ended = takesNext || Complete(arg);
        }

        return null;
    }

    // The inline-code flag itself, "" for python's -m (the module and its arguments follow), or null to go on.
    private static string? Stop(string arg, Regex flags, bool python)
    {
        if (flags.IsMatch(arg) || _inlineLongFlags.Contains(arg))
        {
            return arg;
        }

        return python && arg.StartsWith("-m", StringComparison.Ordinal) ? string.Empty : null;
    }

    // An option that surely takes no further argument: a known flag, or an option with its value attached.
    private static bool Complete(string arg) =>
        arg.Contains('=', StringComparison.Ordinal) || _knownLongFlags.Contains(arg) || KnownFlag.IsMatch(arg);

    private static bool OwnOption(string arg) => !string.Equals(arg, "--", StringComparison.Ordinal) && (arg.StartsWith('-') || arg.StartsWith('+'));

    private static string Family(string program) => program switch
    {
        "sh" or "bash" or "zsh" or "dash" or "ksh" or "fish" => "shell",
        "deno" or "bun" => "node",
        _ => program,
    };

    // Short flags that take no value, and short options with their value attached, across the interpreters: python
    // -BuIsS / -Xdev, ruby -w / -Itest, perl -w / -Ilib, shells -eux / +x, php -n.
    [GeneratedRegex("^-[bBdEhiIOPqsSuvVxwWlTtXnaCHmkp]+\\z|^\\+[eEuxvnhfaBCHmbkpPt]+\\z|^-[XWIrCEFMmdcz].+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KnownFlag { get; }

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
